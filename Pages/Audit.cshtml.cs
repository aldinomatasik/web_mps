using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebMPS.Core.Interfaces.Services;
using WebMPS.Extensions;
using WebMPS.Models;

namespace WebMPS.Pages;

[Authorize(Roles = AppRoles.Admin)]
public class AuditModel : PageModel
{
    private readonly IMpsService _mpsService;
    private readonly ILogger<AuditModel> _logger;

    public AuditModel(IMpsService mpsService, ILogger<AuditModel> logger)
    {
        _mpsService = mpsService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? To { get; set; } = "ALL";

    [BindProperty(SupportsGet = true)]
    public string? Planner { get; set; } = "ALL";

    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; } = "ALL";

    [BindProperty(SupportsGet = true)]
    public string? Date { get; set; }

    public string TodayDate => DateTime.Today.ToString("yyyy-MM-dd");

    public string FormattedDateInput
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Date) || Date.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            if (DateTime.TryParse(Date, out var dt))
                return dt.ToString("yyyy-MM-dd");

            if (DateTime.TryParseExact(Date, new[] { "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "dd-MM-yyyy" },
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dtExact))
                return dtExact.ToString("yyyy-MM-dd");

            return Date;
        }
    }

    [BindProperty(SupportsGet = true)]
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 12;
    public int TotalSlips { get; set; }
    public int TotalPages { get; set; }

    public List<string> DistinctToList { get; set; } = new();
    public List<string> DistinctPlanners { get; set; } = new();
    public List<string> DistinctCategories { get; set; } = new();
    public List<MpsSlip> Slips { get; set; } = new();

    public void OnGet()
    {
        if (!Request.Query.ContainsKey("Date") && string.IsNullOrWhiteSpace(Date))
        {
            Date = TodayDate;
        }
        else if (string.IsNullOrWhiteSpace(Date))
        {
            Date = "ALL";
        }

        DistinctToList = _mpsService.GetDistinctToLocations();
        DistinctPlanners = _mpsService.GetDistinctPlanners();
        DistinctCategories = _mpsService.GetDistinctCategories();

        if (PageIndex < 1) PageIndex = 1;

        var (slips, totalCount) = _mpsService.GetFullyAuditedSlips(To, Planner, Category, Date, PageIndex, PageSize);
        TotalSlips = totalCount;
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalSlips / (double)PageSize));

        if (PageIndex > TotalPages)
        {
            PageIndex = TotalPages;
            (slips, _) = _mpsService.GetFullyAuditedSlips(To, Planner, Category, Date, PageIndex, PageSize);
        }

        Slips = slips;
    }
}
