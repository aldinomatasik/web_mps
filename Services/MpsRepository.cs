using WebMPS.Core.Interfaces.Services;
using WebMPS.Models;

namespace WebMPS.Services;

/// <summary>
/// Facade service providing high-level repository access and backward compatibility
/// for Razor Pages and Minimal API endpoints.
/// </summary>
public class MpsRepository
{
    private readonly IMpsService _mpsService;

    public MpsRepository(IMpsService mpsService)
    {
        _mpsService = mpsService;
    }

    public List<MpsSlip> GetAllSlips() => _mpsService.GetAllSlips();

    public SlipSummaryStats GetSummaryStats() => _mpsService.GetSummaryStats();

    public MpsSlip? GetSlipById(string id) => _mpsService.GetSlipById(id);

    public List<string> GetDistinctToLocations() => _mpsService.GetDistinctToLocations();

    public List<string> GetDistinctPlanners() => _mpsService.GetDistinctPlanners();

    public void AddOrReplaceSlips(IEnumerable<MpsSlip> newSlips, string fileName, byte[] pdfBytes, string uploadedBy = "Operator")
    {
        using var ms = new MemoryStream(pdfBytes);
        _mpsService.ProcessPdfUpload(ms, fileName, pdfBytes, uploadedBy);
    }

    public bool ApproveSlip(string id, string approverName, string role, string? notes) =>
        _mpsService.ApproveSlip(id, approverName, role, notes);

    public int BatchApprove(IEnumerable<string> ids, string approverName, string role, string? notes) =>
        _mpsService.BatchApprove(ids, approverName, role, notes);

    public bool ResetApproval(string id) => _mpsService.ResetApproval(id);

    public List<UploadRecord> GetUploadRecords() => _mpsService.GetUploadHistory();

    public List<ApprovalLog> GetApprovalLogs(int limit = 100) => _mpsService.GetRecentApprovalLogs(limit);

    public List<ApprovalLog> GetApprovalLogsBySlipId(string slipId) => _mpsService.GetApprovalLogsForSlip(slipId);

    public byte[]? GetOriginalPdf(string fileName) => _mpsService.GetOriginalPdf(fileName);

    public byte[]? GetSingleSlipPdf(string slipId) => _mpsService.GetSingleSlipPdf(slipId);

    public byte[]? GetAllSplitPdf(string fileName) => _mpsService.GetAllSplitPdf(fileName);

    public void ClearAll() => _mpsService.ClearAllData();
}
