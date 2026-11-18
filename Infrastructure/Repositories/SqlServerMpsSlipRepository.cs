using System.Text;
using System.Text.Json;
using Dapper;
using WebMPS.Core.Interfaces.Repositories;
using WebMPS.Infrastructure.Data;
using WebMPS.Models;

namespace WebMPS.Infrastructure.Repositories;

public class SqlServerMpsSlipRepository : IMpsSlipRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SqlServerMpsSlipRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public List<MpsSlip> GetAll()
    {
        using var conn = _connectionFactory.CreateConnection();
        var rows = conn.Query("SELECT * FROM MpsSlips ORDER BY OriginalPage, Quadrant;");
        var list = new List<MpsSlip>();
        foreach (var r in rows)
        {
            list.Add(MapSlip(r));
        }
        return list;
    }

    public SlipSummaryStats GetSummaryStats(string? planner = null, string? toLocation = null)
    {
        var whereClause = new StringBuilder("WHERE 1=1");
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(planner) && !planner.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            whereClause.Append(" AND LTRIM(RTRIM(Planner)) = @Planner");
            parameters.Add("Planner", planner.Trim());
        }

        if (!string.IsNullOrWhiteSpace(toLocation) && !toLocation.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            whereClause.Append(" AND LTRIM(RTRIM(ToLocation)) = @ToLocation");
            parameters.Add("ToLocation", toLocation.Trim());
        }

        using var conn = _connectionFactory.CreateConnection();
        var row = conn.QueryFirstOrDefault($@"
            SELECT 
                COUNT(*) AS TotalCount,
                ISNULL(SUM(CASE WHEN ApprovalStatus = 'Approved' THEN 1 ELSE 0 END), 0) AS ApprovedCount,
                ISNULL(SUM(CASE WHEN ApprovalStatus = 'Approved' AND (IsScanned = 0 OR IsScanned IS NULL) THEN 1 ELSE 0 END), 0) AS WaitingHandoverCount
            FROM MpsSlips
            {whereClause};
        ", parameters);

        if (row == null) return new SlipSummaryStats();

        return new SlipSummaryStats
        {
            TotalCount = (int)row.TotalCount,
            ApprovedCount = (int)row.ApprovedCount,
            WaitingHandoverCount = (int)row.WaitingHandoverCount
        };
    }

    public (List<MpsSlip> Slips, int TotalCount) GetPaged(
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
        var whereClause = new StringBuilder("WHERE 1=1");
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(from) && !from.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            whereClause.Append(" AND LTRIM(RTRIM(FromLocation)) = @FromLoc");
            parameters.Add("FromLoc", from.Trim());
        }

        if (!string.IsNullOrWhiteSpace(to) && !to.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            whereClause.Append(" AND LTRIM(RTRIM(ToLocation)) = @ToLoc");
            parameters.Add("ToLoc", to.Trim());
        }

        if (!string.IsNullOrWhiteSpace(planner) && !planner.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            whereClause.Append(" AND LTRIM(RTRIM(Planner)) = @Planner");
            parameters.Add("Planner", planner.Trim());
        }

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            whereClause.Append(" AND LTRIM(RTRIM(Tag)) = @Category");
            parameters.Add("Category", category.Trim());
        }

        if (!string.IsNullOrWhiteSpace(date) && !date.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var trimmedDate = date.Trim();
            if (DateTime.TryParse(trimmedDate, out var dt))
            {
                var f1 = dt.ToString("dd/MM/yyyy");
                var f2 = dt.ToString("MM/dd/yyyy");
                var f3 = dt.ToString("yyyy-MM-dd");
                var f4 = dt.ToString("d/M/yyyy");
                var f5 = dt.ToString("dd-MM-yyyy");
                var f6 = dt.ToString("dd.MM.yyyy");
                var likePattern = $"%{dt.Day:D2}%{dt.Month:D2}%{dt.Year}%";
                var likePattern2 = $"%{dt.Year}%{dt.Month:D2}%{dt.Day:D2}%";

                whereClause.Append(@" AND (
                    SupplyDate = @DateExact OR 
                    SupplyDate IN (@F1, @F2, @F3, @F4, @F5, @F6) OR 
                    SupplyDate LIKE @Like1 OR 
                    SupplyDate LIKE @Like2 OR
                    ReqDate = @DateExact OR 
                    ReqDate IN (@F1, @F2, @F3, @F4, @F5, @F6)
                )");
                parameters.Add("DateExact", trimmedDate);
                parameters.Add("F1", f1);
                parameters.Add("F2", f2);
                parameters.Add("F3", f3);
                parameters.Add("F4", f4);
                parameters.Add("F5", f5);
                parameters.Add("F6", f6);
                parameters.Add("Like1", likePattern);
                parameters.Add("Like2", likePattern2);
            }
            else
            {
                whereClause.Append(" AND (SupplyDate = @DateExact OR SupplyDate LIKE @DateLike OR ReqDate = @DateExact)");
                parameters.Add("DateExact", trimmedDate);
                parameters.Add("DateLike", $"%{trimmedDate}%");
            }
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            whereClause.Append(" AND ApprovalStatus = @Status");
            parameters.Add("Status", status.Trim());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            whereClause.Append(@" AND (
                SlipNumber LIKE @SearchLike OR
                PartCode LIKE @SearchLike OR
                PartDescription LIKE @SearchLike OR
                Planner LIKE @SearchLike OR
                FromLocation LIKE @SearchLike OR
                ToLocation LIKE @SearchLike OR
                PeggedOrdersJson LIKE @SearchLike
            )");
            parameters.Add("SearchLike", $"%{search.Trim()}%");
        }

        if (pageIndex < 1) pageIndex = 1;
        int offset = (pageIndex - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        string sql = $@"
            SELECT COUNT(*) FROM MpsSlips {whereClause};
            SELECT * FROM MpsSlips {whereClause}
            ORDER BY OriginalPage, Quadrant
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
        ";

        using var conn = _connectionFactory.CreateConnection();
        using var multi = conn.QueryMultiple(sql, parameters);
        int totalCount = multi.ReadSingle<int>();
        var rows = multi.Read();

        var list = new List<MpsSlip>();
        foreach (var r in rows)
        {
            list.Add(MapSlip(r));
        }

        return (list, totalCount);
    }

    public (List<MpsSlip> Slips, int TotalCount) GetFullyAudited(
        string? to,
        string? planner,
        string? category,
        string? date,
        int pageIndex,
        int pageSize,
        string? search = null)
    {
        var whereClause = new StringBuilder(@"
            WHERE ApprovalStatus = 'Approved'
              AND DeliverySignature IS NOT NULL AND LTRIM(RTRIM(DeliverySignature)) <> ''
              AND ReceiverSignature IS NOT NULL AND LTRIM(RTRIM(ReceiverSignature)) <> ''
        ");
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(to) && !to.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            whereClause.Append(" AND LTRIM(RTRIM(ToLocation)) = @ToLoc");
            parameters.Add("ToLoc", to.Trim());
        }

        if (!string.IsNullOrWhiteSpace(planner) && !planner.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            whereClause.Append(" AND LTRIM(RTRIM(Planner)) = @Planner");
            parameters.Add("Planner", planner.Trim());
        }

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            whereClause.Append(" AND LTRIM(RTRIM(Tag)) = @Category");
            parameters.Add("Category", category.Trim());
        }

        if (!string.IsNullOrWhiteSpace(date) && !date.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var trimmedDate = date.Trim();
            if (DateTime.TryParse(trimmedDate, out var dt))
            {
                var f1 = dt.ToString("dd/MM/yyyy");
                var f2 = dt.ToString("MM/dd/yyyy");
                var f3 = dt.ToString("yyyy-MM-dd");
                var f4 = dt.ToString("d/M/yyyy");
                var f5 = dt.ToString("dd-MM-yyyy");
                var f6 = dt.ToString("dd.MM.yyyy");
                var likePattern = $"%{dt.Day:D2}%{dt.Month:D2}%{dt.Year}%";
                var likePattern2 = $"%{dt.Year}%{dt.Month:D2}%{dt.Day:D2}%";

                whereClause.Append(@" AND (
                    SupplyDate = @DateExact OR 
                    SupplyDate IN (@F1, @F2, @F3, @F4, @F5, @F6) OR 
                    SupplyDate LIKE @Like1 OR 
                    SupplyDate LIKE @Like2 OR
                    ReqDate = @DateExact OR 
                    ReqDate IN (@F1, @F2, @F3, @F4, @F5, @F6)
                )");
                parameters.Add("DateExact", trimmedDate);
                parameters.Add("F1", f1);
                parameters.Add("F2", f2);
                parameters.Add("F3", f3);
                parameters.Add("F4", f4);
                parameters.Add("F5", f5);
                parameters.Add("F6", f6);
                parameters.Add("Like1", likePattern);
                parameters.Add("Like2", likePattern2);
            }
            else
            {
                whereClause.Append(" AND (SupplyDate = @DateExact OR SupplyDate LIKE @DateLike OR ReqDate = @DateExact)");
                parameters.Add("DateExact", trimmedDate);
                parameters.Add("DateLike", $"%{trimmedDate}%");
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            whereClause.Append(@" AND (
                SlipNumber LIKE @SearchLike OR
                PartCode LIKE @SearchLike OR
                PartDescription LIKE @SearchLike OR
                Planner LIKE @SearchLike OR
                ToLocation LIKE @SearchLike
            )");
            parameters.Add("SearchLike", $"%{search.Trim()}%");
        }

        if (pageIndex < 1) pageIndex = 1;
        int offset = (pageIndex - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        string sql = $@"
            SELECT COUNT(*) FROM MpsSlips {whereClause};
            SELECT * FROM MpsSlips {whereClause}
            ORDER BY ApprovedAt DESC, OriginalPage, Quadrant
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
        ";

        using var conn = _connectionFactory.CreateConnection();
        using var multi = conn.QueryMultiple(sql, parameters);
        int totalCount = multi.ReadSingle<int>();
        var rows = multi.Read();

        var list = new List<MpsSlip>();
        foreach (var r in rows)
        {
            list.Add(MapSlip(r));
        }

        return (list, totalCount);
    }

    public List<MpsSlip> GetApproved(string? planner, string? search, string? status = "Pending")
    {
        var whereClause = new StringBuilder("WHERE ApprovalStatus = 'Approved'");
        var parameters = new DynamicParameters();

        if (string.Equals(status, "Done", StringComparison.OrdinalIgnoreCase))
        {
            whereClause.Append(" AND IsScanned = 1");
        }
        else if (string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            // All approved slips
        }
        else
        {
            // Default "Pending": only slips waiting for handover
            whereClause.Append(" AND (IsScanned = 0 OR IsScanned IS NULL)");
        }

        if (!string.IsNullOrWhiteSpace(planner) && !planner.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            whereClause.Append(" AND LTRIM(RTRIM(Planner)) = @Planner");
            parameters.Add("Planner", planner.Trim());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            whereClause.Append(@" AND (
                SlipNumber LIKE @SearchLike OR
                PartCode LIKE @SearchLike OR
                PartDescription LIKE @SearchLike OR
                Planner LIKE @SearchLike OR
                ApprovedBy LIKE @SearchLike OR
                PeggedOrdersJson LIKE @SearchLike
            )");
            parameters.Add("SearchLike", $"%{search.Trim()}%");
        }

        string sql = $@"
            SELECT * FROM MpsSlips
            {whereClause}
            ORDER BY ApprovedAt DESC, Planner, SlipNumber;
        ";

        using var conn = _connectionFactory.CreateConnection();
        var rows = conn.Query(sql, parameters);
        var list = new List<MpsSlip>();
        foreach (var r in rows)
        {
            list.Add(MapSlip(r));
        }
        return list;
    }

    public MpsSlip? GetById(string id)
    {
        using var conn = _connectionFactory.CreateConnection();
        var row = conn.QueryFirstOrDefault("SELECT * FROM MpsSlips WHERE Id = @Id;", new { Id = id });
        return row != null ? MapSlip(row) : null;
    }

    public List<string> GetDistinctToLocations()
    {
        using var conn = _connectionFactory.CreateConnection();
        return conn.Query<string>(@"
            SELECT DISTINCT LTRIM(RTRIM(ToLocation)) 
            FROM MpsSlips 
            WHERE ToLocation IS NOT NULL AND LTRIM(RTRIM(ToLocation)) <> '' 
            ORDER BY LTRIM(RTRIM(ToLocation));
        ").ToList();
    }

    public List<string> GetDistinctFromLocations()
    {
        using var conn = _connectionFactory.CreateConnection();
        return conn.Query<string>(@"
            SELECT DISTINCT LTRIM(RTRIM(FromLocation)) 
            FROM MpsSlips 
            WHERE FromLocation IS NOT NULL AND LTRIM(RTRIM(FromLocation)) <> '' 
            ORDER BY LTRIM(RTRIM(FromLocation));
        ").ToList();
    }

    public List<string> GetDistinctPlanners()
    {
        using var conn = _connectionFactory.CreateConnection();
        return conn.Query<string>(@"
            SELECT DISTINCT LTRIM(RTRIM(Planner)) 
            FROM MpsSlips 
            WHERE Planner IS NOT NULL AND LTRIM(RTRIM(Planner)) <> '' 
            ORDER BY LTRIM(RTRIM(Planner));
        ").ToList();
    }

    public List<string> GetDistinctCategories()
    {
        using var conn = _connectionFactory.CreateConnection();
        var tags = conn.Query<string>(@"
            SELECT DISTINCT LTRIM(RTRIM(Tag)) 
            FROM MpsSlips 
            WHERE Tag IS NOT NULL AND LTRIM(RTRIM(Tag)) <> ''
            ORDER BY 1;
        ").Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

        // Always ensure standard categories 1A, 1B, 2 are present
        var standards = new[] { "1A", "1B", "2" };
        foreach (var s in standards)
        {
            if (!tags.Any(t => t.Equals(s, StringComparison.OrdinalIgnoreCase)))
            {
                tags.Add(s);
            }
        }
        return tags.OrderBy(t => t).ToList();
    }

    public List<string> GetDistinctSupplyDates()
    {
        using var conn = _connectionFactory.CreateConnection();
        return conn.Query<string>(@"
            SELECT DISTINCT LTRIM(RTRIM(SupplyDate)) 
            FROM MpsSlips 
            WHERE SupplyDate IS NOT NULL AND LTRIM(RTRIM(SupplyDate)) <> '' 
            ORDER BY 1 DESC;
        ").ToList();
    }

    public void InsertRange(IEnumerable<MpsSlip> slips, string uploadId)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var trans = conn.BeginTransaction();

        foreach (var slip in slips)
        {
            slip.UploadId = uploadId;
            var peggedJson = JsonSerializer.Serialize(slip.PeggedOrders);

            conn.Execute(@"
                INSERT INTO MpsSlips (
                    Id, UploadId, OriginalPage, Quadrant, Title, Tag, Planner, SlipNumber,
                    SupplyDate, ReqDate, FromLocation, ToLocation, PartCode, PartDescription,
                    Qty, Unit, IsVmi, VendorCode, VendorName, PeggedOrdersJson, Barcode,
                    ApprovalStatus, DeliverySignature, ReceiverSignature, ApprovedAt, ApprovedBy, Notes, SourceFile,
                    IsScanned, ScannedAt, ScannedBy
                )
                VALUES (
                    @Id, @UploadId, @OriginalPage, @Quadrant, @Title, @Tag, @Planner, @SlipNumber,
                    @SupplyDate, @ReqDate, @FromLocation, @ToLocation, @PartCode, @PartDescription,
                    @Qty, @Unit, @IsVmi, @VendorCode, @VendorName, @PeggedOrdersJson, @Barcode,
                    @ApprovalStatus, @DeliverySignature, @ReceiverSignature, @ApprovedAt, @ApprovedBy, @Notes, @SourceFile,
                    @IsScanned, @ScannedAt, @ScannedBy
                );
            ", new
            {
                slip.Id,
                UploadId = uploadId,
                slip.OriginalPage,
                slip.Quadrant,
                slip.Title,
                slip.Tag,
                slip.Planner,
                slip.SlipNumber,
                slip.SupplyDate,
                slip.ReqDate,
                slip.FromLocation,
                slip.ToLocation,
                slip.PartCode,
                slip.PartDescription,
                slip.Qty,
                slip.Unit,
                IsVmi = slip.IsVmi ? 1 : 0,
                slip.VendorCode,
                slip.VendorName,
                PeggedOrdersJson = peggedJson,
                slip.Barcode,
                slip.ApprovalStatus,
                slip.DeliverySignature,
                slip.ReceiverSignature,
                slip.ApprovedAt,
                slip.ApprovedBy,
                slip.Notes,
                slip.SourceFile,
                IsScanned = slip.IsScanned ? 1 : 0,
                slip.ScannedAt,
                slip.ScannedBy
            }, trans);
        }

        trans.Commit();
    }

    public void DeleteBySourceFile(string fileName)
    {
        using var conn = _connectionFactory.CreateConnection();
        conn.Execute("DELETE FROM MpsSlips WHERE LOWER(SourceFile) = LOWER(@FileName);", new { FileName = fileName });
    }

    public bool UpdateApproval(string id, string approverName, string role, string? notes, DateTime timestamp)
    {
        bool isDelivery = role.Contains("Delivery", StringComparison.OrdinalIgnoreCase);

        using var conn = _connectionFactory.CreateConnection();
        int affected = conn.Execute(@"
            UPDATE MpsSlips SET
                ApprovalStatus = @Status,
                ApprovedBy = @ApprovedBy,
                ApprovedAt = @ApprovedAt,
                DeliverySignature = CASE WHEN @IsDelivery = 1 THEN @ApprovedBy ELSE DeliverySignature END,
                ReceiverSignature = CASE WHEN @IsDelivery = 0 THEN @ApprovedBy ELSE ReceiverSignature END,
                Notes = @Notes
            WHERE Id = @Id;
        ", new
        {
            Id = id,
            Status = ApprovalStatusConstants.Approved,
            ApprovedBy = approverName,
            ApprovedAt = timestamp,
            IsDelivery = isDelivery ? 1 : 0,
            Notes = notes
        });

        return affected > 0;
    }

    public int BatchUpdateApproval(IEnumerable<string> ids, string approverName, string role, string? notes, DateTime timestamp)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return 0;

        bool isDelivery = role.Contains("Delivery", StringComparison.OrdinalIgnoreCase);

        using var conn = _connectionFactory.CreateConnection();
        return conn.Execute(@"
            UPDATE MpsSlips SET
                ApprovalStatus = @Status,
                ApprovedBy = @ApprovedBy,
                ApprovedAt = @ApprovedAt,
                DeliverySignature = CASE WHEN @IsDelivery = 1 THEN @ApprovedBy ELSE DeliverySignature END,
                ReceiverSignature = CASE WHEN @IsDelivery = 0 THEN @ApprovedBy ELSE ReceiverSignature END,
                Notes = COALESCE(@Notes, Notes)
            WHERE Id IN @Ids;
        ", new
        {
            Ids = idList,
            Status = ApprovalStatusConstants.Approved,
            ApprovedBy = approverName,
            ApprovedAt = timestamp,
            IsDelivery = isDelivery ? 1 : 0,
            Notes = notes
        });
    }

    public bool ResetApproval(string id)
    {
        using var conn = _connectionFactory.CreateConnection();
        var affected = conn.Execute(@"
            UPDATE MpsSlips SET
                ApprovalStatus = @Status,
                ApprovedBy = NULL,
                ApprovedAt = NULL,
                DeliverySignature = NULL,
                ReceiverSignature = NULL,
                Notes = NULL,
                IsScanned = 0,
                ScannedAt = NULL,
                ScannedBy = NULL
            WHERE Id = @Id;
        ", new
        {
            Id = id,
            Status = ApprovalStatusConstants.Pending
        });

        return affected > 0;
    }

    public bool UpdateScanStatus(string id, bool isScanned, string? scannedBy, DateTime? timestamp)
    {
        var signer = string.IsNullOrWhiteSpace(scannedBy) ? null : scannedBy.Trim();
        using var conn = _connectionFactory.CreateConnection();
        var affected = conn.Execute(@"
            UPDATE MpsSlips SET
                IsScanned = @IsScanned,
                ScannedBy = CASE WHEN @IsScanned = 1 THEN COALESCE(@ScannedBy, LTRIM(RTRIM(Planner))) ELSE NULL END,
                ScannedAt = CASE WHEN @IsScanned = 1 THEN @ScannedAt ELSE NULL END,
                DeliverySignature = CASE WHEN @IsScanned = 1 THEN COALESCE(@ScannedBy, LTRIM(RTRIM(Planner))) ELSE NULL END
            WHERE Id = @Id;
        ", new
        {
            Id = id,
            IsScanned = isScanned ? 1 : 0,
            ScannedBy = signer,
            ScannedAt = isScanned ? (timestamp ?? DateTime.Now) : (DateTime?)null
        });

        return affected > 0;
    }

    public MpsSlip? GetByBarcodeOrSlipNumber(string barcodeOrSlipNumber)
    {
        var cleaned = barcodeOrSlipNumber.Trim().TrimStart('#');
        using var conn = _connectionFactory.CreateConnection();
        var row = conn.QueryFirstOrDefault(@"
            SELECT * FROM MpsSlips 
            WHERE SlipNumber = @Val 
               OR Barcode = @Val 
               OR Id = @Val;
        ", new { Val = cleaned });

        return row != null ? MapSlip(row) : null;
    }

    public void ClearAll()
    {
        using var conn = _connectionFactory.CreateConnection();
        conn.Execute("DELETE FROM MpsSlips;");
    }

    private static MpsSlip MapSlip(dynamic row)
    {
        var slip = new MpsSlip
        {
            Id = (string)row.Id,
            UploadId = row.UploadId ?? string.Empty,
            OriginalPage = (int)row.OriginalPage,
            Quadrant = (int)row.Quadrant,
            Title = (string)row.Title ?? string.Empty,
            Tag = (string)row.Tag ?? string.Empty,
            Planner = (string)row.Planner ?? string.Empty,
            SlipNumber = (string)row.SlipNumber ?? string.Empty,
            SupplyDate = (string)row.SupplyDate ?? string.Empty,
            ReqDate = (string)row.ReqDate ?? string.Empty,
            FromLocation = (string)row.FromLocation ?? string.Empty,
            ToLocation = (string)row.ToLocation ?? string.Empty,
            PartCode = (string)row.PartCode ?? string.Empty,
            PartDescription = (string)row.PartDescription ?? string.Empty,
            Qty = Convert.ToDecimal(row.Qty),
            Unit = (string)row.Unit ?? "PC",
            IsVmi = (bool)row.IsVmi,
            VendorCode = (string)row.VendorCode ?? string.Empty,
            VendorName = (string)row.VendorName ?? string.Empty,
            ApprovalStatus = (string)row.ApprovalStatus ?? ApprovalStatusConstants.Pending,
            DeliverySignature = (string?)row.DeliverySignature,
            ReceiverSignature = (string?)row.ReceiverSignature,
            ApprovedBy = (string?)row.ApprovedBy,
            Notes = (string?)row.Notes,
            SourceFile = (string)row.SourceFile ?? string.Empty,
            IsScanned = row.IsScanned != null && (bool)row.IsScanned,
            ScannedBy = (string?)row.ScannedBy
        };

        if (row.ScannedAt != null)
        {
            if (row.ScannedAt is DateTime sdt)
            {
                slip.ScannedAt = sdt;
            }
            else if (DateTime.TryParse(row.ScannedAt.ToString(), out DateTime parsedSdt))
            {
                slip.ScannedAt = parsedSdt;
            }
        }

        if (row.ApprovedAt != null)
        {
            if (row.ApprovedAt is DateTime dt)
            {
                slip.ApprovedAt = dt;
            }
            else if (DateTime.TryParse(row.ApprovedAt.ToString(), out DateTime parsedDt))
            {
                slip.ApprovedAt = parsedDt;
            }
        }

        string? peggedJson = (string?)row.PeggedOrdersJson;
        if (!string.IsNullOrEmpty(peggedJson))
        {
            try
            {
                slip.PeggedOrders = JsonSerializer.Deserialize<List<MpsPeggedOrder>>(peggedJson) ?? new();
            }
            catch
            {
                slip.PeggedOrders = new();
            }
        }

        return slip;
    }
}
