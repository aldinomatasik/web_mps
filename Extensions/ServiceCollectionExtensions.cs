using WebMPS.Application.Services;
using WebMPS.Core.Interfaces.Repositories;
using WebMPS.Core.Interfaces.Services;
using WebMPS.Infrastructure.Data;
using WebMPS.Infrastructure.Repositories;
using WebMPS.Infrastructure.Services;
using WebMPS.Services;

namespace WebMPS.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebMpsServices(
        this IServiceCollection services,
        IWebHostEnvironment env,
        IConfiguration config)
    {
        // 1. Connection String & SQL Server Connection Factory (SSMS)
        var connectionString = config.GetConnectionString("DefaultConnection") 
            ?? "Server=localhost\\SQLEXPRESS;Database=WebMPS;Trusted_Connection=True;TrustServerCertificate=True;";

        services.AddSingleton<IDbConnectionFactory>(_ => new SqlServerConnectionFactory(connectionString));
        services.AddSingleton<IDatabaseInitializer, SqlServerDatabaseInitializer>();

        // 2. Repositories (Infrastructure -> Core Interfaces)
        services.AddSingleton<IMpsSlipRepository, SqlServerMpsSlipRepository>();
        services.AddSingleton<IUploadRecordRepository, SqlServerUploadRecordRepository>();
        services.AddSingleton<IApprovalLogRepository, SqlServerApprovalLogRepository>();
        services.AddSingleton<IUserRepository, SqlServerUserRepository>();

        // 3. File & PDF Processing Services
        services.AddSingleton<IPdfStorageService, FileSystemPdfStorageService>();
        services.AddSingleton<IMpsParserService, MpsParserService>();
        services.AddSingleton<IMpsPdfSplitterService, MpsPdfSplitterService>();
        services.AddSingleton<IBarcodeService, Code39BarcodeService>();

        // Concrete services for backward compatibility
        services.AddSingleton<MpsParserService>();
        services.AddSingleton<MpsPdfSplitterService>();

        // 4. Application Services
        services.AddSingleton<IMpsService, MpsService>();
        services.AddSingleton<IAuthService, AuthService>();

        // 5. Facade Service (Presentation / Razor Pages adapter)
        services.AddSingleton<MpsRepository>();

        return services;
    }
}
