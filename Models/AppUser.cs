namespace WebMPS.Models;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Uploader = "Uploader";
    public const string TO = "TO";
    public const string Planner = "Planner";
}

public class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = AppRoles.Uploader;
    public string? AssignedScope { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
