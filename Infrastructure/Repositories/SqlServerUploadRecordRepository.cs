using Dapper;
using WebMPS.Core.Interfaces.Repositories;
using WebMPS.Infrastructure.Data;
using WebMPS.Models;

namespace WebMPS.Infrastructure.Repositories;

public class SqlServerUploadRecordRepository : IUploadRecordRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SqlServerUploadRecordRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public List<UploadRecord> GetAll()
    {
        using var conn = _connectionFactory.CreateConnection();
        var rows = conn.Query("SELECT * FROM UploadRecords ORDER BY UploadedAt DESC;");
        var list = new List<UploadRecord>();
        foreach (var r in rows)
        {
            list.Add(MapRecord(r));
        }
        return list;
    }

    public UploadRecord? GetById(string id)
    {
        using var conn = _connectionFactory.CreateConnection();
        var r = conn.QueryFirstOrDefault("SELECT * FROM UploadRecords WHERE Id = @Id;", new { Id = id });
        return r != null ? MapRecord(r) : null;
    }

    public void Insert(UploadRecord record)
    {
        using var conn = _connectionFactory.CreateConnection();
        conn.Execute(@"
            INSERT INTO UploadRecords (Id, FileName, FileSizeBytes, TotalSlips, UploadedAt, UploadedBy, Status)
            VALUES (@Id, @FileName, @FileSizeBytes, @TotalSlips, @UploadedAt, @UploadedBy, @Status);
        ", new
        {
            record.Id,
            record.FileName,
            record.FileSizeBytes,
            record.TotalSlips,
            record.UploadedAt,
            record.UploadedBy,
            record.Status
        });
    }

    public void ClearAll()
    {
        using var conn = _connectionFactory.CreateConnection();
        conn.Execute("DELETE FROM UploadRecords;");
    }

    private static UploadRecord MapRecord(dynamic r)
    {
        var rec = new UploadRecord
        {
            Id = (string)r.Id,
            FileName = (string)r.FileName,
            FileSizeBytes = (long)r.FileSizeBytes,
            TotalSlips = (int)r.TotalSlips,
            UploadedBy = (string?)r.UploadedBy ?? "System",
            Status = (string?)r.Status ?? "Success"
        };

        if (r.UploadedAt != null)
        {
            if (r.UploadedAt is DateTime dt)
            {
                rec.UploadedAt = dt;
            }
            else if (DateTime.TryParse(r.UploadedAt.ToString(), out DateTime parsedDt))
            {
                rec.UploadedAt = parsedDt;
            }
        }

        return rec;
    }
}
