using WebMPS.Models;

namespace WebMPS.Core.Interfaces.Services;

public interface IMpsService
{
    List<MpsSlip> GetAllSlips();
    SlipSummaryStats GetSummaryStats(string? planner = null, string? toLocation = null);
    (List<MpsSlip> Slips, int TotalCount) GetPagedSlips(string? from, string? to, string? status, string? search, int pageIndex, int pageSize, string? planner = null, string? category = null, string? date = null);
    (List<MpsSlip> Slips, int TotalCount) GetFullyAuditedSlips(string? to, string? planner, string? category, string? date, int pageIndex, int pageSize, string? search = null);
    List<MpsSlip> GetApprovedSlips(string? planner, string? search, string? status = "Pending");
    MpsSlip? GetSlipById(string id);
    List<string> GetDistinctToLocations();
    List<string> GetDistinctFromLocations();
    List<string> GetDistinctPlanners();
    List<string> GetDistinctCategories();
    List<string> GetDistinctSupplyDates();
    List<UploadRecord> GetUploadHistory();
    List<ApprovalLog> GetRecentApprovalLogs(int limit = 100);
    List<ApprovalLog> GetApprovalLogsForSlip(string slipId);

    int ProcessPdfUpload(Stream stream, string fileName, byte[] bytes, string uploadedBy = "Operator");
    bool ApproveSlip(string slipId, string approverName, string role, string? notes);
    int BatchApprove(IEnumerable<string> slipIds, string approverName, string role, string? notes);
    bool ResetApproval(string slipId, string resetBy = "Admin");

    bool SetScanStatus(string slipId, bool isScanned, string scannedBy);
    (bool success, string message, MpsSlip? slip) ScanBarcode(string barcodeOrSlipNumber, string scannedBy);

    byte[]? GetOriginalPdf(string fileName);
    byte[]? GetSingleSlipPdf(string slipId);
    byte[]? GetAllSplitPdf(string? fileName = null, string? toLocation = null);

    void ClearAllData();
}
