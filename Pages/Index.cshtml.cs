using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebMPS.Core.Interfaces.Services;
using WebMPS.Extensions;
using WebMPS.Models;

namespace WebMPS.Pages;

[Authorize]
[RequestSizeLimit(104_857_600)]
[RequestFormLimits(MultipartBodyLengthLimit = 104_857_600)]
public class IndexModel : PageModel
{
    private readonly IMpsService _mpsService;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<IndexModel> _logger;

    [BindProperty]
    public List<IFormFile> UploadFiles { get; set; } = new();

    [BindProperty]
    public IFormFile? UploadFile { get; set; }

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public List<MpsSlip> CurrentSlips { get; set; } = new();
    public List<string> DistinctToList { get; set; } = new();
    public List<string> DistinctPlanners { get; set; } = new();
    public List<UploadRecord> UploadHistory { get; set; } = new();

    public IndexModel(
        IMpsService mpsService,
        IWebHostEnvironment env,
        ILogger<IndexModel> logger)
    {
        _mpsService = mpsService;
        _env = env;
        _logger = logger;
    }

    private void RefreshState()
    {
        UploadHistory = _mpsService.GetUploadHistory();
    }

    public IActionResult OnGet()
    {
        if (User.IsInRole(AppRoles.TO))
            return RedirectToPage("/Slips");

        if (User.IsInRole(AppRoles.Planner))
            return RedirectToPage("/Approval");

        if (!User.IsInRole(AppRoles.Admin) && !User.IsInRole(AppRoles.Uploader))
            return RedirectToPage("/AccessDenied");

        RefreshState();
        return Page();
    }

    public async Task<IActionResult> OnPostUploadAsync()
    {
        if (!User.IsInRole(AppRoles.Admin) && !User.IsInRole(AppRoles.Uploader))
            return Forbid();

        var uploadedBy = User.GetDisplayName();
        var filesToProcess = new List<IFormFile>();
        if (UploadFiles != null && UploadFiles.Count > 0)
        {
            filesToProcess.AddRange(UploadFiles.Where(f => f != null && f.Length > 0));
        }
        if (UploadFile != null && UploadFile.Length > 0)
        {
            filesToProcess.Add(UploadFile);
        }

        if (filesToProcess.Count == 0)
        {
            ErrorMessage = "Please select or drag & drop at least one Material Picking Slip (MPS) PDF file.";
            RefreshState();
            return Page();
        }

        var seenFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var succeeded = new List<(string FileName, int SlipCount)>();
        var failed = new List<string>();
        int totalSlips = 0;

        foreach (var file in filesToProcess)
        {
            var safeFileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                failed.Add("(unnamed file)");
                continue;
            }

            if (!seenFileNames.Add(safeFileName))
            {
                continue;
            }

            if (!safeFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                failed.Add($"{safeFileName} (must be .pdf)");
                continue;
            }

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var bytes = ms.ToArray();

                ms.Position = 0;
                int count = _mpsService.ProcessPdfUpload(ms, safeFileName, bytes, uploadedBy);

                if (count == 0)
                {
                    failed.Add($"{safeFileName} (no valid MPS slips found)");
                }
                else
                {
                    succeeded.Add((safeFileName, count));
                    totalSlips += count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading and parsing PDF {FileName}.", safeFileName);
                failed.Add($"{safeFileName} ({ex.Message})");
            }
        }

        if (succeeded.Count == 1)
        {
            SuccessMessage = $"Successfully processed {succeeded[0].FileName}! {succeeded[0].SlipCount} individual slips saved to the database.";
        }
        else if (succeeded.Count > 1)
        {
            var summaryDetails = string.Join(", ", succeeded.Select(s => $"{s.FileName}: {s.SlipCount}"));
            SuccessMessage = $"Successfully processed {succeeded.Count} PDF files! Total {totalSlips} individual slips saved ({summaryDetails}).";
        }

        if (failed.Count > 0)
        {
            ErrorMessage = $"Failed to process {failed.Count} file(s): {string.Join("; ", failed)}";
        }

        RefreshState();
        return Page();
    }

    public IActionResult OnPostReloadDefault()
    {
        if (!User.IsInRole(AppRoles.Admin) && !User.IsInRole(AppRoles.Uploader))
            return Forbid();

        try
        {
            var defaultFile = Path.Combine(_env.ContentRootPath, "MPS_05112026 120937 PM.pdf");
            if (!System.IO.File.Exists(defaultFile))
            {
                ErrorMessage = "Default file MPS_05112026 120937 PM.pdf not found on server.";
                RefreshState();
                return Page();
            }

            var bytes = System.IO.File.ReadAllBytes(defaultFile);
            using var ms = new MemoryStream(bytes);
            int count = _mpsService.ProcessPdfUpload(ms, "MPS_05112026 120937 PM.pdf", bytes, User.GetDisplayName());

            SuccessMessage = $"Successfully reloaded default file! {count} slips ready.";
            RefreshState();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reloading default PDF.");
            ErrorMessage = $"Failed to reload default file: {ex.Message}";
            RefreshState();
            return Page();
        }
    }

    public IActionResult OnPostClear()
    {
        if (!User.IsInRole(AppRoles.Admin))
            return Forbid();

        _mpsService.ClearAllData();
        SuccessMessage = "Database cleared successfully.";
        RefreshState();
        return Page();
    }
}
