using Dapper;
using WebMPS.Core.Interfaces.Repositories;
using WebMPS.Infrastructure.Data;
using WebMPS.Models;

namespace WebMPS.Infrastructure.Repositories;

public class SqlServerApprovalLogRepository : IApprovalLogRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SqlServerApprovalLogRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public List<ApprovalLog> GetRecent(int limit = 100)
    {
        using var conn = _connectionFactory.CreateConnection();
        var rows = conn.Query(@"
            SELECT TOP (@Limit) * FROM ApprovalLogs 
            ORDER BY Id DESC;
        ", new { Limit = limit });

        return MapRows(rows);
    }

    public List<ApprovalLog> GetBySlipId(string slipId)
    {
        using var conn = _connectionFactory.CreateConnection();
        var rows = conn.Query(@"
            SELECT * FROM ApprovalLogs 
            WHERE SlipId = @SlipId 
            ORDER BY Id DESC;
        ", new { SlipId = slipId });

        return MapRows(rows);
    }

    public void Insert(ApprovalLog log)
    {
        using var conn = _connectionFactory.CreateConnection();
        conn.Execute(@"
            INSERT INTO ApprovalLogs (SlipId, SlipNumber, Action, ApproverName, Role, Notes, Timestamp)
            VALUES (@SlipId, @SlipNumber, @Action, @ApproverName, @Role, @Notes, @Timestamp);
        ", new
        {
            log.SlipId,
            log.SlipNumber,
            log.Action,
            log.ApproverName,
            log.Role,
            log.Notes,
            log.Timestamp
        });
    }

    public void InsertRange(IEnumerable<ApprovalLog> logs)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();

        foreach (var log in logs)
        {
            conn.Execute(@"
                INSERT INTO ApprovalLogs (SlipId, SlipNumber, Action, ApproverName, Role, Notes, Timestamp)
                VALUES (@SlipId, @SlipNumber, @Action, @ApproverName, @Role, @Notes, @Timestamp);
            ", new
            {
                log.SlipId,
                log.SlipNumber,
                log.Action,
                log.ApproverName,
                log.Role,
                log.Notes,
                log.Timestamp
            }, trans);
        }

        trans.Commit();
    }

    public void ClearAll()
    {
        using var conn = _connectionFactory.CreateConnection();
        conn.Execute("DELETE FROM ApprovalLogs;");
    }

    private static List<ApprovalLog> MapRows(IEnumerable<dynamic> rows)
    {
        var list = new List<ApprovalLog>();
        foreach (var r in rows)
        {
            var log = new ApprovalLog
            {
                Id = (int)r.Id,
                SlipId = (string)r.SlipId,
                SlipNumber = (string?)r.SlipNumber ?? string.Empty,
                Action = (string)r.Action,
                ApproverName = (string)r.ApproverName,
                Role = (string)r.Role,
                Notes = (string?)r.Notes
            };

            if (r.Timestamp != null)
            {
                if (r.Timestamp is DateTime dt)
                {
                    log.Timestamp = dt;
                }
                else if (DateTime.TryParse(r.Timestamp.ToString(), out DateTime parsedDt))
                {
                    log.Timestamp = parsedDt;
                }
            }

            list.Add(log);
        }
        return list;
    }
}
