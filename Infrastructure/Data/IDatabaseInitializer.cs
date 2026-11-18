using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Identity;
using WebMPS.Core.Interfaces.Services;
using WebMPS.Models;

namespace WebMPS.Infrastructure.Data;

public interface IDatabaseInitializer
{
    void Initialize();
}

public class SqlServerDatabaseInitializer : IDatabaseInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IMpsParserService _parser;
    private readonly IPdfStorageService _pdfStorage;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SqlServerDatabaseInitializer> _logger;

    public SqlServerDatabaseInitializer(
        IDbConnectionFactory connectionFactory,
        IMpsParserService parser,
        IPdfStorageService pdfStorage,
        IWebHostEnvironment env,
        ILogger<SqlServerDatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _parser = parser;
        _pdfStorage = pdfStorage;
        _env = env;
        _logger = logger;
    }

    public void Initialize()
    {
        try
        {
            using var conn = _connectionFactory.CreateConnection();

            // 1. Create Tables in SQL Server (SSMS)
            conn.Execute(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UploadRecords' AND xtype='U')
                CREATE TABLE UploadRecords (
                    Id NVARCHAR(50) PRIMARY KEY,
                    FileName NVARCHAR(255) NOT NULL,
                    FileSizeBytes BIGINT NOT NULL,
                    TotalSlips INT NOT NULL,
                    UploadedAt DATETIME2 NOT NULL,
                    UploadedBy NVARCHAR(100),
                    Status NVARCHAR(50) NOT NULL
                );

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='MpsSlips' AND xtype='U')
                CREATE TABLE MpsSlips (
                    Id NVARCHAR(50) PRIMARY KEY,
                    UploadId NVARCHAR(50),
                    OriginalPage INT NOT NULL,
                    Quadrant INT NOT NULL,
                    Title NVARCHAR(255),
                    Tag NVARCHAR(50),
                    Planner NVARCHAR(100),
                    SlipNumber NVARCHAR(100),
                    SupplyDate NVARCHAR(50),
                    ReqDate NVARCHAR(50),
                    FromLocation NVARCHAR(100),
                    ToLocation NVARCHAR(100),
                    PartCode NVARCHAR(100),
                    PartDescription NVARCHAR(255),
                    Qty DECIMAL(18, 3) NOT NULL,
                    Unit NVARCHAR(50),
                    IsVmi BIT NOT NULL,
                    VendorCode NVARCHAR(100),
                    VendorName NVARCHAR(255),
                    PeggedOrdersJson NVARCHAR(MAX),
                    Barcode NVARCHAR(100),
                    ApprovalStatus NVARCHAR(50) NOT NULL,
                    DeliverySignature NVARCHAR(100),
                    ReceiverSignature NVARCHAR(100),
                    ApprovedAt DATETIME2,
                    ApprovedBy NVARCHAR(100),
                    Notes NVARCHAR(MAX),
                    SourceFile NVARCHAR(255) NOT NULL,
                    IsScanned BIT NOT NULL DEFAULT 0,
                    ScannedAt DATETIME2,
                    ScannedBy NVARCHAR(100)
                );

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MpsSlips') AND name = 'IsScanned')
                BEGIN
                    ALTER TABLE MpsSlips ADD IsScanned BIT NOT NULL DEFAULT 0;
                    ALTER TABLE MpsSlips ADD ScannedAt DATETIME2 NULL;
                    ALTER TABLE MpsSlips ADD ScannedBy NVARCHAR(100) NULL;
                END

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ApprovalLogs' AND xtype='U')
                CREATE TABLE ApprovalLogs (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    SlipId NVARCHAR(50) NOT NULL,
                    SlipNumber NVARCHAR(100),
                    Action NVARCHAR(100) NOT NULL,
                    ApproverName NVARCHAR(100) NOT NULL,
                    Role NVARCHAR(100) NOT NULL,
                    Notes NVARCHAR(MAX),
                    Timestamp DATETIME2 NOT NULL
                );

                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MpsSlips_SlipNumber')
                    CREATE INDEX IX_MpsSlips_SlipNumber ON MpsSlips(SlipNumber);

                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MpsSlips_ToLocation')
                    CREATE INDEX IX_MpsSlips_ToLocation ON MpsSlips(ToLocation);

                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MpsSlips_ApprovalStatus')
                    CREATE INDEX IX_MpsSlips_ApprovalStatus ON MpsSlips(ApprovalStatus);

                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ApprovalLogs_SlipId')
                    CREATE INDEX IX_ApprovalLogs_SlipId ON ApprovalLogs(SlipId);

                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ApprovalLogs_Timestamp')
                    CREATE INDEX IX_ApprovalLogs_Timestamp ON ApprovalLogs(Timestamp);

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
                CREATE TABLE Users (
                    Id NVARCHAR(50) PRIMARY KEY,
                    Username NVARCHAR(100) NOT NULL,
                    PasswordHash NVARCHAR(MAX) NOT NULL,
                    DisplayName NVARCHAR(100) NOT NULL,
                    Role NVARCHAR(50) NOT NULL,
                    AssignedScope NVARCHAR(100),
                    IsActive BIT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
                );

                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Users_Username')
                    CREATE UNIQUE INDEX IX_Users_Username ON Users(Username);

                UPDATE MpsSlips
                SET DeliverySignature = LTRIM(RTRIM(Planner)),
                    ScannedBy = LTRIM(RTRIM(Planner)),
                    ScannedAt = COALESCE(ScannedAt, ApprovedAt, GETDATE())
                WHERE IsScanned = 1 AND (DeliverySignature IS NULL OR LTRIM(RTRIM(DeliverySignature)) = '');
            ");

            SeedUsersIfMissing(conn);

            int slipCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM MpsSlips;");

            // 2. Initial Seeding if empty
            if (slipCount == 0)
            {
                SeedFromExistingJsonOrPdf(conn);
            }

            // Ensure default PDF file is cached in storage if available
            CacheDefaultPdfIfExists();
            _logger.LogInformation("SQL Server (SSMS) database initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SQL Server database.");
            throw;
        }
    }

    private static void SeedUsersIfMissing(IDbConnection conn)
    {
        var hasher = new PasswordHasher<AppUser>();
        var defaults = new (string Username, string Password, string DisplayName, string Role, string? Scope)[]
        {
            ("admin", "admin123", "Administrator", AppRoles.Admin, null),
            ("uploader", "uploader123", "Uploader", AppRoles.Uploader, null),
            ("csline", "123456", "H100 (CS LINE)", AppRoles.TO, "H100 (CS LINE)"),
            ("hendro", "123456", "R01 (HENDRO)", AppRoles.Planner, "R01 (HENDRO)"),
            ("feby", "123456", "R02 (FEBY)", AppRoles.Planner, "R02 (FEBY)"),
            ("suroto", "123456", "R04 (SUROTO)", AppRoles.Planner, "R04 (SUROTO)"),
            ("masnuri", "123456", "R06 (MASNURI)", AppRoles.Planner, "R06 (MASNURI)"),
            ("hidayatullah", "123456", "R07 (HIDAYATULLAH)", AppRoles.Planner, "R07 (HIDAYATULLAH)"),
            ("taufiq", "123456", "R08 (TAUFIQ ZAINURI)", AppRoles.Planner, "R08 (TAUFIQ ZAINURI)"),
            ("ali", "123456", "R12 (ALI AKBAR)", AppRoles.Planner, "R12 (ALI AKBAR)")
        };

        var existing = new HashSet<string>(
            conn.Query<string>("SELECT Username FROM Users;"),
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in defaults)
        {
            if (existing.Contains(item.Username)) continue;

            var user = new AppUser
            {
                Id = Guid.NewGuid().ToString("N"),
                Username = item.Username,
                DisplayName = item.DisplayName,
                Role = item.Role,
                AssignedScope = item.Scope,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            user.PasswordHash = hasher.HashPassword(user, item.Password);

            conn.Execute(@"
                INSERT INTO Users (Id, Username, PasswordHash, DisplayName, Role, AssignedScope, IsActive, CreatedAt)
                VALUES (@Id, @Username, @PasswordHash, @DisplayName, @Role, @AssignedScope, @IsActive, @CreatedAt);
            ", new
            {
                user.Id,
                user.Username,
                user.PasswordHash,
                user.DisplayName,
                user.Role,
                user.AssignedScope,
                IsActive = 1,
                user.CreatedAt
            });
        }
    }

    private void SeedFromExistingJsonOrPdf(IDbConnection conn)
    {
        var storageDir = Path.Combine(_env.ContentRootPath, "App_Data");
        var jsonFile = Path.Combine(storageDir, "mps_slips.json");

        if (File.Exists(jsonFile))
        {
            try
            {
                var json = File.ReadAllText(jsonFile);
                var loaded = JsonSerializer.Deserialize<List<MpsSlip>>(json);
                if (loaded != null && loaded.Count > 0)
                {
                    var defaultFileName = loaded.FirstOrDefault()?.SourceFile ?? "MPS_05112026 120937 PM.pdf";
                    var defaultPdfPath = Path.Combine(_env.ContentRootPath, defaultFileName);
                    long fileSize = File.Exists(defaultPdfPath) ? new FileInfo(defaultPdfPath).Length : 0;

                    var uploadRecord = new UploadRecord
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        FileName = defaultFileName,
                        FileSizeBytes = fileSize,
                        TotalSlips = loaded.Count,
                        UploadedAt = DateTime.Now,
                        UploadedBy = "Initial Seed (JSON)",
                        Status = "Success"
                    };

                    InsertUploadRecord(conn, uploadRecord);
                    InsertSlips(conn, loaded, uploadRecord.Id);
                    _logger.LogInformation("Successfully seeded {Count} slips into SQL Server.", loaded.Count);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "JSON migration skipped or failed. Falling back to default PDF.");
            }
        }

        var defaultPdf = Path.Combine(_env.ContentRootPath, "MPS_05112026 120937 PM.pdf");
        if (File.Exists(defaultPdf))
        {
            try
            {
                var bytes = File.ReadAllBytes(defaultPdf);
                _pdfStorage.SaveOriginalPdf("MPS_05112026 120937 PM.pdf", bytes);

                using var ms = new MemoryStream(bytes);
                var parsed = _parser.ParsePdf(ms, "MPS_05112026 120937 PM.pdf");

                var uploadRecord = new UploadRecord
                {
                    Id = Guid.NewGuid().ToString("N"),
                    FileName = "MPS_05112026 120937 PM.pdf",
                    FileSizeBytes = bytes.Length,
                    TotalSlips = parsed.Count,
                    UploadedAt = DateTime.Now,
                    UploadedBy = "Default Seed (PDF)",
                    Status = "Success"
                };

                InsertUploadRecord(conn, uploadRecord);
                InsertSlips(conn, parsed, uploadRecord.Id);
                _logger.LogInformation("Successfully auto-parsed and seeded {Count} slips into SQL Server.", parsed.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed default MPS PDF into SQL Server database.");
            }
        }
    }

    private void CacheDefaultPdfIfExists()
    {
        var defaultFile = Path.Combine(_env.ContentRootPath, "MPS_05112026 120937 PM.pdf");
        if (File.Exists(defaultFile))
        {
            if (_pdfStorage.GetOriginalPdf("MPS_05112026 120937 PM.pdf") == null)
            {
                _pdfStorage.SaveOriginalPdf("MPS_05112026 120937 PM.pdf", File.ReadAllBytes(defaultFile));
            }
        }
    }

    private static void InsertUploadRecord(IDbConnection conn, UploadRecord record)
    {
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

    private static void InsertSlips(IDbConnection conn, IEnumerable<MpsSlip> slips, string uploadId)
    {
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
                    ApprovalStatus, DeliverySignature, ReceiverSignature, ApprovedAt, ApprovedBy, Notes, SourceFile
                )
                VALUES (
                    @Id, @UploadId, @OriginalPage, @Quadrant, @Title, @Tag, @Planner, @SlipNumber,
                    @SupplyDate, @ReqDate, @FromLocation, @ToLocation, @PartCode, @PartDescription,
                    @Qty, @Unit, @IsVmi, @VendorCode, @VendorName, @PeggedOrdersJson, @Barcode,
                    @ApprovalStatus, @DeliverySignature, @ReceiverSignature, @ApprovedAt, @ApprovedBy, @Notes, @SourceFile
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
                slip.SourceFile
            }, trans);
        }
        trans.Commit();
    }
}
