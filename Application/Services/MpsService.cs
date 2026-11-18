using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using WebMPS.Core.Interfaces.Repositories;
using WebMPS.Core.Interfaces.Services;
using WebMPS.Models;

namespace WebMPS.Application.Services;

public class MpsService : IMpsService
{
    private readonly IMpsSlipRepository _slipRepo;
    private readonly IUploadRecordRepository _uploadRepo;
    private readonly IApprovalLogRepository _logRepo;
    private readonly IPdfStorageService _pdfStorage;
    private readonly IMpsParserService _parser;
    private readonly IMpsPdfSplitterService _splitter;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<MpsService> _logger;
    private readonly IMemoryCache _cache;

    private const string CacheStatsKey = "WebMPS_SummaryStats";
    private const string CacheFromKey = "WebMPS_DistinctFrom";
    private const string CacheToKey = "WebMPS_DistinctTo";
    private const string CachePlannerKey = "WebMPS_DistinctPlanners";

    public MpsService(
        IMpsSlipRepository slipRepo,
        IUploadRecordRepository uploadRepo,
        IApprovalLogRepository logRepo,
        IPdfStorageService pdfStorage,
        IMpsParserService parser,
        IMpsPdfSplitterService splitter,
        IWebHostEnvironment env,
        ILogger<MpsService> logger,
        IMemoryCache cache)
    {
        _slipRepo = slipRepo;
        _uploadRepo = uploadRepo;
        _logRepo = logRepo;
        _pdfStorage = pdfStorage;
        _parser = parser;
        _splitter = splitter;
        _env = env;
        _logger = logger;
        _cache = cache;
    }

    public List<MpsSlip> GetAllSlips()
    {
        return _slipRepo.GetAll();
    }

    public SlipSummaryStats GetSummaryStats(string? planner = null, string? toLocation = null)
    {
        bool isFilteredByPlanner = !string.IsNullOrWhiteSpace(planner) && !planner.Equals("ALL", StringComparison.OrdinalIgnoreCase);
        bool isFilteredByTo = !string.IsNullOrWhiteSpace(toLocation) && !toLocation.Equals("ALL", StringComparison.OrdinalIgnoreCase);

        if (isFilteredByPlanner || isFilteredByTo)
        {
            return _slipRepo.GetSummaryStats(planner, toLocation);
        }

        return _cache.GetOrCreate(CacheStatsKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
            return _slipRepo.GetSummaryStats();
        }) ?? new SlipSummaryStats();
    }

    public (List<MpsSlip> Slips, int TotalCount) GetPagedSlips(
        string? from,
        string? to,
        string? status,
        string? search,
        int pageIndex,
        int pageSize,
        string? planner = null,
        string? category = null,
        string? date = null)
    {
        return _slipRepo.GetPaged(from, to, status, search, pageIndex, pageSize, planner, category, date);
    }

    public (List<MpsSlip> Slips, int TotalCount) GetFullyAuditedSlips(
        string? to,
        string? planner,
        string? category,
        string? date,
        int pageIndex,
        int pageSize,
        string? search = null)
    {
        return _slipRepo.GetFullyAudited(to, planner, category, date, pageIndex, pageSize, search);
    }

    public List<MpsSlip> GetApprovedSlips(string? planner, string? search, string? status = "Pending")
    {
        return _slipRepo.GetApproved(planner, search, status);
    }

    public MpsSlip? GetSlipById(string id)
    {
        return _slipRepo.GetById(id);
    }

    public List<string> GetDistinctToLocations()
    {
        return _cache.GetOrCreate(CacheToKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return _slipRepo.GetDistinctToLocations();
        }) ?? new List<string>();
    }

    public List<string> GetDistinctFromLocations()
    {
        return _cache.GetOrCreate(CacheFromKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return _slipRepo.GetDistinctFromLocations();
        }) ?? new List<string>();
    }

    public List<string> GetDistinctPlanners()
    {
        return _cache.GetOrCreate(CachePlannerKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return _slipRepo.GetDistinctPlanners();
        }) ?? new List<string>();
    }

    public List<string> GetDistinctCategories()
    {
        return _slipRepo.GetDistinctCategories();
    }

    public List<string> GetDistinctSupplyDates()
    {
        return _slipRepo.GetDistinctSupplyDates();
    }

    public List<UploadRecord> GetUploadHistory()
    {
        return _uploadRepo.GetAll();
    }

    public List<ApprovalLog> GetRecentApprovalLogs(int limit = 100)
    {
        return _logRepo.GetRecent(limit);
    }

    public List<ApprovalLog> GetApprovalLogsForSlip(string slipId)
    {
        return _logRepo.GetBySlipId(slipId);
    }

    public int ProcessPdfUpload(Stream stream, string fileName, byte[] bytes, string uploadedBy = "Operator")
    {
        _pdfStorage.SaveOriginalPdf(fileName, bytes);

        var parsedSlips = _parser.ParsePdf(stream, fileName);
        if (parsedSlips.Count == 0)
        {
            return 0;
        }

        _slipRepo.DeleteBySourceFile(fileName);

        var uploadRecord = new UploadRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            FileName = fileName,
            FileSizeBytes = bytes.Length,
            TotalSlips = parsedSlips.Count,
            UploadedAt = DateTime.Now,
            UploadedBy = uploadedBy,
            Status = "Success"
        };
        _uploadRepo.Insert(uploadRecord);
        _slipRepo.InsertRange(parsedSlips, uploadRecord.Id);

        InvalidateAllCaches();

        _logger.LogInformation("Successfully processed and recorded upload: {FileName} ({Count} slips).", fileName, parsedSlips.Count);
        return parsedSlips.Count;
    }

    public bool ApproveSlip(string slipId, string approverName, string role, string? notes)
    {
        var timestamp = DateTime.Now;
        bool ok = _slipRepo.UpdateApproval(slipId, approverName, role, notes, timestamp);

        if (ok)
        {
            _cache.Remove(CacheStatsKey);

            // Log asynchronously or fast insert without blocking JSON disk sync
            var slip = _slipRepo.GetById(slipId);
            if (slip != null)
            {
                _logRepo.Insert(new ApprovalLog
                {
                    SlipId = slipId,
                    SlipNumber = slip.SlipNumber,
                    Action = ApprovalStatusConstants.Approved,
                    ApproverName = approverName,
                    Role = role,
                    Notes = notes,
                    Timestamp = timestamp
                });
            }
        }

        return ok;
    }

    public int BatchApprove(IEnumerable<string> slipIds, string approverName, string role, string? notes)
    {
        var idList = slipIds.ToList();
        if (idList.Count == 0) return 0;

        var timestamp = DateTime.Now;
        int count = _slipRepo.BatchUpdateApproval(idList, approverName, role, notes, timestamp);

        if (count > 0)
        {
            _cache.Remove(CacheStatsKey);
        }

        return count;
    }

    public bool ResetApproval(string slipId, string resetBy = "Admin")
    {
        var slip = _slipRepo.GetById(slipId);
        if (slip == null) return false;

        bool ok = _slipRepo.ResetApproval(slipId);
        if (ok)
        {
            _cache.Remove(CacheStatsKey);

            _logRepo.Insert(new ApprovalLog
            {
                SlipId = slipId,
                SlipNumber = slip.SlipNumber,
                Action = "Reset Approval",
                ApproverName = string.IsNullOrWhiteSpace(resetBy) ? "Admin" : resetBy,
                Role = "Admin",
                Notes = "Status reverted to Pending",
                Timestamp = DateTime.Now
            });
        }

        return ok;
    }

    public bool SetScanStatus(string slipId, bool isScanned, string scannedBy)
    {
        var now = DateTime.Now;
        bool ok = _slipRepo.UpdateScanStatus(slipId, isScanned, scannedBy, now);
        if (ok)
        {
            _cache.Remove(CacheStatsKey);
        }
        return ok;
    }

    public (bool success, string message, MpsSlip? slip) ScanBarcode(string barcodeOrSlipNumber, string scannedBy)
    {
        if (string.IsNullOrWhiteSpace(barcodeOrSlipNumber))
        {
            return (false, "Barcode or slip number cannot be empty.", null);
        }

        var slip = _slipRepo.GetByBarcodeOrSlipNumber(barcodeOrSlipNumber);
        if (slip == null)
        {
            return (false, $"Slip '{barcodeOrSlipNumber}' not found.", null);
        }

        var now = DateTime.Now;
        bool ok = _slipRepo.UpdateScanStatus(slip.Id, true, scannedBy, now);
        if (ok)
        {
            _cache.Remove(CacheStatsKey);
            slip.IsScanned = true;
            slip.ScannedAt = now;
            slip.ScannedBy = scannedBy;
            slip.DeliverySignature = scannedBy;
            return (true, $"Slip #{slip.SlipNumber} marked as ALREADY SCANNED.", slip);
        }

        return (false, "Failed to update scan status.", null);
    }

    public byte[]? GetOriginalPdf(string fileName)
    {
        return _pdfStorage.GetOriginalPdf(fileName);
    }

    public byte[]? GetSingleSlipPdf(string slipId)
    {
        var slip = _slipRepo.GetById(slipId);
        if (slip == null) return null;

        return _splitter.CreateSingleSlipPdf(Stream.Null, slip.OriginalPage, slip.Quadrant, slip);
    }

    public byte[]? GetAllSplitPdf(string? fileName = null, string? toLocation = null)
    {
        var filteredSlips = _slipRepo.GetAll().AsEnumerable();

        if (!string.IsNullOrWhiteSpace(toLocation))
        {
            var trimmedTo = toLocation.Trim();
            filteredSlips = filteredSlips.Where(s =>
                string.Equals(s.ToLocation?.Trim(), trimmedTo, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            filteredSlips = filteredSlips.Where(s =>
                s.SourceFile.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        }

        var list = filteredSlips.ToList();
        if (list.Count > 0)
        {
            return _splitter.CreateSplitPdf(Stream.Null, list);
        }

        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var origBytes = _pdfStorage.GetOriginalPdf(fileName);
        if (origBytes == null) return null;

        using var ms = new MemoryStream(origBytes);
        return _splitter.CreateSplitPdf(ms, list);
    }

    public void ClearAllData()
    {
        _slipRepo.ClearAll();
        _uploadRepo.ClearAll();
        _logRepo.ClearAll();
        _pdfStorage.Clear();
        InvalidateAllCaches();
    }

    private void InvalidateAllCaches()
    {
        _cache.Remove(CacheStatsKey);
        _cache.Remove(CacheFromKey);
        _cache.Remove(CacheToKey);
        _cache.Remove(CachePlannerKey);
    }
}
