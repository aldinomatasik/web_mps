using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebMPS.Core.Interfaces.Services;
using WebMPS.Extensions;
using WebMPS.Models;

namespace WebMPS.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly IAuthService _authService;

    public LoginModel(IAuthService authService)
    {
        _authService = authService;
    }

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public bool RememberMe { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage(User.GetHomePage());
        }

        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = _authService.Authenticate(Username, Password);
        if (user == null)
        {
            ErrorMessage = "Invalid username or password.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
            new(AppClaimTypes.DisplayName, user.DisplayName),
            new(AppClaimTypes.AssignedScope, user.AssignedScope ?? string.Empty)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = RememberMe,
            ExpiresUtc = RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(12)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            authProperties);

        if (!string.IsNullOrWhiteSpace(ReturnUrl) &&
            Url.IsLocalUrl(ReturnUrl) &&
            IsReturnUrlAllowedForRole(user.Role, ReturnUrl))
        {
            return LocalRedirect(ReturnUrl);
        }

        return RedirectToPage(principal.GetHomePage());
    }

    private static bool IsReturnUrlAllowedForRole(string role, string returnUrl)
    {
        var path = returnUrl.Split('?')[0].TrimEnd('/').ToLowerInvariant();
        if (string.IsNullOrEmpty(path)) path = "/index";

        return role switch
        {
            AppRoles.Admin => true,
            AppRoles.Uploader => path == "/index",
            AppRoles.TO => path == "/slips",
            AppRoles.Planner => path == "/approval",
            _ => false
        };
    }
}
