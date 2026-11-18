using WebMPS.Models;

namespace WebMPS.Core.Interfaces.Repositories;

public interface IMpsSlipRepository
{
    List<MpsSlip> GetAll();
    SlipSummaryStats GetSummaryStats(string? planner = null, string? toLocation = null);
    (List<MpsSlip> Slips, int TotalCount) GetPaged(string? from, string? to, string? status, string? search, int pageIndex, int pageSize, string? planner = null, string? category = null, string? date = null);
    (List<MpsSlip> Slips, int TotalCount) GetFullyAudited(string? to, string? planner, string? category, string? date, int pageIndex, int pageSize, string? search = null);
    List<MpsSlip> GetApproved(string? planner, string? search, string? status = "Pending");
    MpsSlip? GetById(string id);
    List<string> GetDistinctToLocations();
    List<string> GetDistinctFromLocations();
    List<string> GetDistinctPlanners();
    List<string> GetDistinctCategories();
    List<string> GetDistinctSupplyDates();
    void InsertRange(IEnumerable<MpsSlip> slips, string uploadId);
    void DeleteBySourceFile(string fileName);
    bool UpdateApproval(string id, string approverName, string role, string? notes, DateTime timestamp);
    int BatchUpdateApproval(IEnumerable<string> ids, string approverName, string role, string? notes, DateTime timestamp);
    bool ResetApproval(string id);
    bool UpdateScanStatus(string id, bool isScanned, string? scannedBy, DateTime? timestamp);
    MpsSlip? GetByBarcodeOrSlipNumber(string barcodeOrSlipNumber);
    void ClearAll();
}
