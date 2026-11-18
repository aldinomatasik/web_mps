using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebMPS.Core.Interfaces.Services;
using WebMPS.Extensions;
using WebMPS.Models;

namespace WebMPS.Pages;

[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Planner}")]
public class ApprovalModel : PageModel
{
    private readonly IMpsService _mpsService;
    private readonly ILogger<ApprovalModel> _logger;

    public ApprovalModel(IMpsService mpsService, ILogger<ApprovalModel> logger)
    {
        _mpsService = mpsService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? Planner { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; } = "Pending";

    public string? FeedbackMessage { get; set; }
    public string? FeedbackType { get; set; }

    public List<string> DistinctPlanners { get; set; } = new();
    public List<MpsSlip> Slips { get; set; } = new();

    public int TotalCount { get; set; }
    public int ApprovedCount { get; set; }
    public int WaitingHandoverCount { get; set; }
    public int PendingCount { get; set; }

    public void OnGet()
    {
        LoadData();
    }

    private void LoadData()
    {
        if (string.IsNullOrWhiteSpace(Status))
        {
            Status = "Pending";
        }

        bool isPlanner = User.IsInRole(AppRoles.Planner);
        if (isPlanner)
        {
            var scopedPlanner = User.GetAssignedScope();
            Planner = string.IsNullOrWhiteSpace(scopedPlanner) ? "__NO_SCOPE__" : scopedPlanner;
            DistinctPlanners = string.IsNullOrWhiteSpace(scopedPlanner)
                ? new List<string>()
                : new List<string> { scopedPlanner };
        }
        else
        {
            DistinctPlanners = _mpsService.GetDistinctPlanners();
        }

        var stats = isPlanner
            ? _mpsService.GetSummaryStats(planner: Planner)
            : _mpsService.GetSummaryStats();

        TotalCount = stats.TotalCount;
        PendingCount = stats.PendingCount;
        ApprovedCount = stats.ApprovedCount;
        WaitingHandoverCount = stats.WaitingHandoverCount;

        Slips = _mpsService.GetApprovedSlips(Planner, Search, Status);
    }

    public IActionResult OnPostToggleDone(string slipId)
    {
        var slip = _mpsService.GetSlipById(slipId);
        if (slip == null || !User.CanAccessSlip(slip))
        {
            return Forbid();
        }

        bool newStatus = !slip.IsScanned;
        var signerName = User.GetDisplayName();
        _mpsService.SetScanStatus(slipId, newStatus, signerName);

        FeedbackMessage = newStatus
            ? $"Slip #{slip.SlipNumber} marked as Done ({signerName})."
            : $"Slip #{slip.SlipNumber} unmarked.";
        FeedbackType = newStatus ? "success" : "info";

        LoadData();
        return Page();
    }

    public IActionResult OnPostToggleDoneAjax(string slipId)
    {
        var slip = _mpsService.GetSlipById(slipId);
        if (slip == null || !User.CanAccessSlip(slip))
        {
            return new JsonResult(new { success = false });
        }

        bool newStatus = !slip.IsScanned;
        var signerName = User.GetDisplayName();
        bool ok = _mpsService.SetScanStatus(slipId, newStatus, signerName);

        return new JsonResult(new
        {
            success = ok,
            slipId,
            isScanned = newStatus,
            signedBy = signerName
        });
    }

    public IActionResult OnPostReset(string slipId)
    {
        if (!User.IsInRole(AppRoles.Admin))
        {
            return Forbid();
        }

        _mpsService.ResetApproval(slipId, User.GetDisplayName());
        FeedbackMessage = "Slip approval reverted to Pending status.";
        FeedbackType = "info";

        LoadData();
        return Page();
    }
}
