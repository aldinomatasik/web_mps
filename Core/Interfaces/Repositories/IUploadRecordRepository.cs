using WebMPS.Models;

namespace WebMPS.Core.Interfaces.Repositories;

public interface IUploadRecordRepository
{
    List<UploadRecord> GetAll();
    UploadRecord? GetById(string id);
    void Insert(UploadRecord record);
    void ClearAll();
}
