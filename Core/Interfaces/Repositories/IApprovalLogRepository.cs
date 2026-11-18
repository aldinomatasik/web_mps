using WebMPS.Models;

namespace WebMPS.Core.Interfaces.Repositories;

public interface IApprovalLogRepository
{
    List<ApprovalLog> GetRecent(int limit = 100);
    List<ApprovalLog> GetBySlipId(string slipId);
    void Insert(ApprovalLog log);
    void InsertRange(IEnumerable<ApprovalLog> logs);
    void ClearAll();
}
