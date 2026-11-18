using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebMPS.Core.Interfaces.Repositories;
using WebMPS.Core.Interfaces.Services;
using WebMPS.Models;

namespace WebMPS.Pages;

[Authorize(Roles = AppRoles.Admin)]
public class UsersModel : PageModel
{
    private readonly IUserRepository _userRepo;
    private readonly IAuthService _authService;
    private readonly IMpsService _mpsService;

    public UsersModel(IUserRepository userRepo, IAuthService authService, IMpsService mpsService)
    {
        _userRepo = userRepo;
        _authService = authService;
        _mpsService = mpsService;
    }

    [BindProperty(SupportsGet = true)]
    public string? RoleFilter { get; set; } = "ALL";

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [TempData]
    public string? FeedbackMessage { get; set; }

    [TempData]
    public string? FeedbackType { get; set; }

    public List<AppUser> Users { get; set; } = new();
    public List<string> DistinctToLocations { get; set; } = new();
    public List<string> DistinctPlanners { get; set; } = new();

    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int FieldStaffCount { get; set; }
    public int AdminUploaderCount { get; set; }
    public string CurrentUserId { get; set; } = string.Empty;

    public void OnGet()
    {
        CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        DistinctToLocations = _mpsService.GetDistinctToLocations();
        DistinctPlanners = _mpsService.GetDistinctPlanners();

        var allUsers = _userRepo.GetAll();
        TotalUsers = allUsers.Count;
        ActiveUsers = allUsers.Count(u => u.IsActive);
        FieldStaffCount = allUsers.Count(u => u.Role == AppRoles.TO || u.Role == AppRoles.Planner);
        AdminUploaderCount = allUsers.Count(u => u.Role == AppRoles.Admin || u.Role == AppRoles.Uploader);

        IEnumerable<AppUser> query = allUsers;
        if (!string.IsNullOrWhiteSpace(RoleFilter) && !RoleFilter.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(u => u.Role.Equals(RoleFilter.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim();
            query = query.Where(u =>
                u.Username.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                u.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(u.AssignedScope) && u.AssignedScope.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        Users = query
            .OrderBy(u => GetRoleOrder(u.Role))
            .ThenBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IActionResult OnPostCreate(
        string username,
        string password,
        string displayName,
        string role,
        string? assignedScope,
        bool isActive = true)
    {
        username = (username ?? string.Empty).Trim();
        displayName = (displayName ?? string.Empty).Trim();
        assignedScope = string.IsNullOrWhiteSpace(assignedScope) ? null : assignedScope.Trim();

        if (string.IsNullOrWhiteSpace(username) || username.Any(char.IsWhiteSpace))
        {
            FeedbackMessage = "Username is required and cannot contain spaces.";
            FeedbackType = "danger";
            return RedirectToPage(new { RoleFilter, Search });
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
        {
            FeedbackMessage = "Password must be at least 4 characters.";
            FeedbackType = "danger";
            return RedirectToPage(new { RoleFilter, Search });
        }

        if (!IsValidRole(role))
        {
            FeedbackMessage = "Invalid role selected.";
            FeedbackType = "danger";
            return RedirectToPage(new { RoleFilter, Search });
        }

        if (_userRepo.GetByUsername(username) != null)
        {
            FeedbackMessage = $"Username '{username}' is already taken.";
            FeedbackType = "danger";
            return RedirectToPage(new { RoleFilter, Search });
        }

        if ((role == AppRoles.TO || role == AppRoles.Planner) && string.IsNullOrWhiteSpace(assignedScope))
        {
            FeedbackMessage = $"Assigned Scope is required for role {role}.";
            FeedbackType = "danger";
            return RedirectToPage(new { RoleFilter, Search });
        }

        if (role == AppRoles.Admin || role == AppRoles.Uploader)
        {
            assignedScope = null;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = assignedScope ?? username;
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = username,
            DisplayName = displayName,
            Role = role,
            AssignedScope = assignedScope,
            IsActive = isActive,
            CreatedAt = DateTime.Now
        };
        user.PasswordHash = _authService.HashPassword(user, password);

        _userRepo.Insert(user);

        FeedbackMessage = $"Account '@{user.Username}' ({user.Role}) created successfully!";
        FeedbackType = "success";
        return RedirectToPage(new { RoleFilter, Search });
    }

    public IActionResult OnPostEdit(
        string id,
        string username,
        string? password,
        string displayName,
        string role,
        string? assignedScope,
        bool isActive = false)
    {
        var existing = _userRepo.GetById(id);
        if (existing == null)
        {
            FeedbackMessage = "User not found.";
            FeedbackType = "danger";
            return RedirectToPage(new { RoleFilter, Search });
        }

        username = (username ?? string.Empty).Trim();
        displayName = (displayName ?? string.Empty).Trim();
        assignedScope = string.IsNullOrWhiteSpace(assignedScope) ? null : assignedScope.Trim();

        if (string.IsNullOrWhiteSpace(username) || username.Any(char.IsWhiteSpace))
        {
            FeedbackMessage = "Username is required and cannot contain spaces.";
            FeedbackType = "danger";
            return RedirectToPage(new { RoleFilter, Search });
        }

        if (!IsValidRole(role))
        {
            FeedbackMessage = "Invalid role selected.";
            FeedbackType = "danger";
            return RedirectToPage(new { RoleFilter, Search });
        }

        var currentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (existing.Id == currentId)
        {
            if (role != AppRoles.Admin)
            {
                FeedbackMessage = "You cannot change the role of your own active Admin account.";
                FeedbackType = "warning";
                return RedirectToPage(new { RoleFilter, Search });
            }
            isActive = true;
        }

        var conflict = _userRepo.GetByUsername(username);
        if (conflict != null && conflict.Id != existing.Id)
        {
            FeedbackMessage = $"Username '{username}' is already used by another account.";
            FeedbackType = "danger";
            return RedirectToPage(new { RoleFilter, Search });
        }

        if ((role == AppRoles.TO || role == AppRoles.Planner) && string.IsNullOrWhiteSpace(assignedScope))
        {
            FeedbackMessage = $"Assigned Scope is required for role {role}.";
            FeedbackType = "danger";
            return RedirectToPage(new { RoleFilter, Search });
        }

        if (!string.IsNullOrWhiteSpace(password) && password.Length < 4)
        {
            FeedbackMessage = "New password must be at least 4 characters.";
            FeedbackType = "danger";
            return RedirectToPage(new { RoleFilter, Search });
        }

        if (role == AppRoles.Admin || role == AppRoles.Uploader)
        {
            assignedScope = null;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = assignedScope ?? username;
        }

        existing.Username = username;
        existing.DisplayName = displayName;
        existing.Role = role;
        existing.AssignedScope = assignedScope;
        existing.IsActive = isActive;
        existing.PasswordHash = !string.IsNullOrWhiteSpace(password)
            ? _authService.HashPassword(existing, password)
            : string.Empty;

        _userRepo.Update(existing);

        FeedbackMessage = $"Account '@{existing.Username}' updated successfully!";
        FeedbackType = "success";
        return RedirectToPage(new { RoleFilter, Search });
    }

    public IActionResult OnPostToggleActiveAjax(string id)
    {
        var user = _userRepo.GetById(id);
        if (user == null)
        {
            return new JsonResult(new { success = false, message = "User not found." });
        }

        var currentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (user.Id == currentId)
        {
            return new JsonResult(new { success = false, message = "Cannot deactivate your own account." });
        }

        bool newStatus = !user.IsActive;
        bool ok = _userRepo.UpdateStatus(id, newStatus);
        return new JsonResult(new { success = ok, id, isActive = newStatus });
    }

    public IActionResult OnPostDelete(string id)
    {
        var user = _userRepo.GetById(id);
        if (user == null)
        {
            FeedbackMessage = "User not found.";
            FeedbackType = "warning";
            return RedirectToPage(new { RoleFilter, Search });
        }

        var currentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (user.Id == currentId)
        {
            FeedbackMessage = "You cannot delete your own active Admin account.";
            FeedbackType = "warning";
            return RedirectToPage(new { RoleFilter, Search });
        }

        _userRepo.Delete(id);
        FeedbackMessage = $"Account '@{user.Username}' has been deleted.";
        FeedbackType = "info";
        return RedirectToPage(new { RoleFilter, Search });
    }

    private static bool IsValidRole(string? role) =>
        role == AppRoles.Admin || role == AppRoles.Uploader || role == AppRoles.TO || role == AppRoles.Planner;

    private static int GetRoleOrder(string role) => role switch
    {
        AppRoles.Admin => 1,
        AppRoles.Uploader => 2,
        AppRoles.TO => 3,
        AppRoles.Planner => 4,
        _ => 5
    };
}
