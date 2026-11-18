using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using WebMPS.Core.Interfaces.Services;
using WebMPS.Extensions;
using WebMPS.Infrastructure.Data;
using WebMPS.Models;
using WebMPS.Services;

var builder = WebApplication.CreateBuilder(args);

// Allow up to 100 MB multipart uploads for batch multi-PDF drag & drop (aligned with web.config)
const long MaxUploadBytes = 104_857_600;
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MaxUploadBytes;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = MaxUploadBytes;
});

// 1. Data Protection configuration
var keysFolder = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
Directory.CreateDirectory(keysFolder);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysFolder))
    .SetApplicationName("WebMPS");

// 2. Add MemoryCache & Response Compression (Gzip/Brotli for fast LAN performance)
builder.Services.AddMemoryCache();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

// 3. Add Authentication, Authorization & Razor Pages
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "WebMPS.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.AccessDeniedPath = "/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Logout");
    options.Conventions.AllowAnonymousToPage("/Error");
});

// 4. Register Clean Architecture layers (Core, Infrastructure, Application, Presentation)
builder.Services.AddWebMpsServices(builder.Environment, builder.Configuration);

var app = builder.Build();

// 5. Initialize SQL Server Database & seed data
using (var scope = app.Services.CreateScope())
{
    var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    dbInitializer.Initialize();
}

app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// 6. Static Files with aggressive browser caching for instant page switches
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "public,max-age=604800";
    }
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// 7. Fast JSON API for Single Slip View Modal (eliminates N modal DOM bloat)
app.MapGet("/api/slip/{id}", (string id, IMpsService mpsService, HttpContext httpContext) =>
{
    var slip = mpsService.GetSlipById(id);
    if (slip == null) return Results.NotFound();
    if (!httpContext.User.CanAccessSlip(slip)) return Results.Forbid();

    return Results.Ok(new
    {
        id = slip.Id,
        slipNumber = slip.SlipNumber,
        originalPage = slip.OriginalPage,
        quadrantLabel = slip.QuadrantLabel,
        title = slip.Title,
        tag = slip.Tag,
        planner = slip.Planner,
        supplyDate = slip.SupplyDate,
        reqDate = slip.ReqDate,
        fromLocation = slip.FromLocation,
        toLocation = slip.ToLocation,
        partCode = slip.PartCode,
        partDescription = slip.PartDescription,
        qty = slip.Qty.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
        unit = slip.Unit,
        peggedOrders = slip.PeggedOrders.Select(o => new
        {
            orderNumber = o.OrderNumber,
            peggedMaterial = o.PeggedMaterial,
            reqQty = o.ReqQty.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
            unit = o.Unit
        }),
        deliverySignature = !string.IsNullOrWhiteSpace(slip.DeliverySignature)
            ? slip.DeliverySignature
            : (slip.IsScanned ? slip.Planner : ""),
        deliveryDate = (!string.IsNullOrWhiteSpace(slip.DeliverySignature) || slip.IsScanned)
            ? (slip.ScannedAt ?? slip.ApprovedAt ?? DateTime.Now).ToString("dd/MM/yyyy")
            : "",
        receiverSignature = slip.ReceiverSignature ?? "",
        approvedDate = slip.ApprovedAt?.ToString("dd/MM/yyyy") ?? "",
        isScanned = slip.IsScanned,
        barcodeSvg = BarcodeHelper.GenerateSvgBarcode(slip.Barcode, 26)
    });
}).RequireAuthorization();

// 8. Minimal API Endpoints for PDF Downloads
app.MapGet("/download/slip/{id}", (string id, IMpsService mpsService, HttpContext httpContext) =>
{
    var slip = mpsService.GetSlipById(id);
    if (slip == null)
    {
        return Results.NotFound("Slip not found");
    }
    if (!httpContext.User.CanAccessSlip(slip))
    {
        return Results.Forbid();
    }

    var pdfBytes = mpsService.GetSingleSlipPdf(id);
    if (pdfBytes == null)
    {
        return Results.NotFound("Original PDF file not available for rendering");
    }

    return Results.File(pdfBytes, "application/pdf", $"MPS_Slip_{slip.SlipNumber}.pdf");
}).RequireAuthorization();

app.MapGet("/download/all", (string? fileName, IMpsService mpsService, HttpContext httpContext) =>
{
    var role = httpContext.User.GetRole();
    if (role != AppRoles.Admin && role != AppRoles.TO)
    {
        return Results.Forbid();
    }

    string? scopedTo = role == AppRoles.TO ? httpContext.User.GetAssignedScope() : null;
    if (role == AppRoles.TO && string.IsNullOrWhiteSpace(scopedTo))
    {
        return Results.Forbid();
    }

    var slips = mpsService.GetAllSlips().AsEnumerable();
    if (!string.IsNullOrWhiteSpace(scopedTo))
    {
        slips = slips.Where(s => string.Equals(s.ToLocation?.Trim(), scopedTo, StringComparison.OrdinalIgnoreCase));
    }
    if (!string.IsNullOrWhiteSpace(fileName))
    {
        slips = slips.Where(s => s.SourceFile.Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }

    var targetCount = slips.Count();
    if (targetCount == 0)
    {
        return Results.NotFound("No slips available");
    }

    var pdfBytes = mpsService.GetAllSplitPdf(fileName, scopedTo);
    if (pdfBytes == null)
    {
        return Results.NotFound("Failed to generate split PDF");
    }

    return Results.File(pdfBytes, "application/pdf", $"MPS_Split_All_{targetCount}_Slips.pdf");
}).RequireAuthorization();

app.Run();
