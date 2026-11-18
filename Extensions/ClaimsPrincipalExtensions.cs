using System.Security.Claims;
using WebMPS.Models;

namespace WebMPS.Extensions;

public static class AppClaimTypes
{
    public const string DisplayName = "DisplayName";
    public const string AssignedScope = "AssignedScope";
}

public static class ClaimsPrincipalExtensions
{
    public static string GetRole(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

    public static string GetDisplayName(this ClaimsPrincipal user)
    {
        var displayName = user.FindFirst(AppClaimTypes.DisplayName)?.Value;
        return !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : (user.Identity?.Name ?? "User");
    }

    public static string GetAssignedScope(this ClaimsPrincipal user) =>
        user.FindFirst(AppClaimTypes.AssignedScope)?.Value?.Trim() ?? string.Empty;

    public static string GetHomePage(this ClaimsPrincipal user)
    {
        return user.GetRole() switch
        {
            AppRoles.TO => "/Slips",
            AppRoles.Planner => "/Approval",
            AppRoles.Uploader => "/Index",
            AppRoles.Admin => "/Index",
            _ => "/Login"
        };
    }

    public static bool CanAccessSlip(this ClaimsPrincipal user, MpsSlip? slip)
    {
        if (slip == null) return false;

        var role = user.GetRole();
        if (role == AppRoles.Admin) return true;

        var scope = user.GetAssignedScope();
        if (string.IsNullOrWhiteSpace(scope)) return false;

        return role switch
        {
            AppRoles.TO => string.Equals(slip.ToLocation?.Trim(), scope, StringComparison.OrdinalIgnoreCase),
            AppRoles.Planner => string.Equals(slip.Planner?.Trim(), scope, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
