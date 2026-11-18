using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebMPS.Core.Interfaces.Services;
using WebMPS.Extensions;
using WebMPS.Models;

namespace WebMPS.Pages;

[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.TO}")]
public class SlipsModel : PageModel
{
    private readonly IMpsService _mpsService;
    private readonly ILogger<SlipsModel> _logger;

    public SlipsModel(IMpsService mpsService, ILogger<SlipsModel> logger)
    {
        _mpsService = mpsService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? To { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; } = "ALL";

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
    public int PendingCount { get; set; }

    public List<string> DistinctFromList { get; set; } = new();
    public List<string> DistinctToList { get; set; } = new();
    public List<string> DistinctPlanners { get; set; } = new();
    public List<string> DistinctCategories { get; set; } = new();
    public List<string> DistinctDates { get; set; } = new();
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

        DistinctFromList = _mpsService.GetDistinctFromLocations();
        DistinctPlanners = _mpsService.GetDistinctPlanners();
        DistinctCategories = _mpsService.GetDistinctCategories();
        DistinctDates = _mpsService.GetDistinctSupplyDates();

        if (User.IsInRole(AppRoles.TO))
        {
            var scopedTo = User.GetAssignedScope();
            To = string.IsNullOrWhiteSpace(scopedTo) ? "__NO_SCOPE__" : scopedTo;
            DistinctToList = string.IsNullOrWhiteSpace(scopedTo)
                ? new List<string>()
                : new List<string> { scopedTo };
        }
        else
        {
            DistinctToList = _mpsService.GetDistinctToLocations();
        }

        if (PageIndex < 1) PageIndex = 1;

        var (slips, totalCount) = _mpsService.GetPagedSlips(From, To, Status, Search, PageIndex, PageSize, Planner, Category, Date);
        TotalSlips = totalCount;
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalSlips / (double)PageSize));

        if (PageIndex > TotalPages)
        {
            PageIndex = TotalPages;
            (slips, _) = _mpsService.GetPagedSlips(From, To, Status, Search, PageIndex, PageSize, Planner, Category, Date);
        }

        Slips = slips;
        PendingCount = Slips.Count(s => s.ApprovalStatus != ApprovalStatusConstants.Approved);
    }

    public IActionResult OnPostQuickApprove(string slipId)
    {
        var slip = _mpsService.GetSlipById(slipId);
        if (slip == null || !User.CanAccessSlip(slip))
        {
            return Forbid();
        }

        var approverName = User.GetDisplayName();
        const string role = "Receiver";

        _mpsService.ApproveSlip(slipId, approverName, role, "Approved from slip card");
        return RedirectToPage(new { To, Planner, Category, Date, Search, Status, PageIndex });
    }

    public IActionResult OnPostBatchApprove([FromForm] string[] slipIds)
    {
        if (slipIds != null && slipIds.Length > 0)
        {
            var approverName = User.GetDisplayName();
            const string role = "Receiver";

            var validIds = slipIds
                .Select(id => _mpsService.GetSlipById(id))
                .Where(s => s != null && User.CanAccessSlip(s) && s.ApprovalStatus != ApprovalStatusConstants.Approved)
                .Select(s => s!.Id)
                .ToList();

            if (validIds.Count > 0)
            {
                _mpsService.BatchApprove(validIds, approverName, role, "Batch approved by receiver");
            }
        }
        return RedirectToPage(new { To, Planner, Category, Date, Status, Search, PageIndex });
    }

    public IActionResult OnPostBatchApproveAjax([FromForm] string[] slipIds)
    {
        if (slipIds == null || slipIds.Length == 0)
        {
            return new JsonResult(new { success = false, message = "No slips selected." });
        }

        var approverName = User.GetDisplayName();
        const string role = "Receiver";

        var validIds = new List<string>();
        foreach (var id in slipIds)
        {
            var slip = _mpsService.GetSlipById(id);
            if (slip != null && User.CanAccessSlip(slip) && slip.ApprovalStatus != ApprovalStatusConstants.Approved)
            {
                validIds.Add(id);
            }
        }

        if (validIds.Count == 0)
        {
            return new JsonResult(new { success = false, message = "No valid pending slips found to approve." });
        }

        int count = _mpsService.BatchApprove(validIds, approverName, role, "Batch approved by receiver");
        var stats = User.IsInRole(AppRoles.TO)
            ? _mpsService.GetSummaryStats(toLocation: User.GetAssignedScope())
            : _mpsService.GetSummaryStats();

        return new JsonResult(new
        {
            success = count > 0,
            approvedCount = count,
            approvedIds = validIds,
            approvedBy = approverName,
            approvedDate = DateTime.Now.ToString("dd/MM/yyyy"),
            stats = new
            {
                totalCount = stats.TotalCount,
                pendingCount = stats.PendingCount,
                approvedCount = stats.ApprovedCount
            }
        });
    }

    public IActionResult OnPostQuickApproveAjax(string slipId)
    {
        var slip = _mpsService.GetSlipById(slipId);
        if (slip == null || !User.CanAccessSlip(slip))
        {
            return new JsonResult(new { success = false });
        }

        var approverName = User.GetDisplayName();
        const string role = "Receiver";

        bool ok = _mpsService.ApproveSlip(slipId, approverName, role, "Approved from slip card");
        var stats = User.IsInRole(AppRoles.TO)
            ? _mpsService.GetSummaryStats(toLocation: User.GetAssignedScope())
            : _mpsService.GetSummaryStats();

        return new JsonResult(new
        {
            success = ok,
            slipId,
            approvedBy = approverName,
            approvedDate = DateTime.Now.ToString("dd/MM/yyyy"),
            stats = new
            {
                totalCount = stats.TotalCount,
                pendingCount = stats.PendingCount,
                approvedCount = stats.ApprovedCount
            }
        });
    }
}
