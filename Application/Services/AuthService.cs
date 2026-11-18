using Microsoft.AspNetCore.Identity;
using WebMPS.Core.Interfaces.Repositories;
using WebMPS.Core.Interfaces.Services;
using WebMPS.Models;

namespace WebMPS.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly PasswordHasher<AppUser> _hasher = new();

    public AuthService(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    public string HashPassword(AppUser user, string password)
    {
        return _hasher.HashPassword(user, password);
    }

    public AppUser? Authenticate(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        var user = _userRepo.GetByUsername(username);
        if (user == null || !user.IsActive)
            return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Success ||
               result == PasswordVerificationResult.SuccessRehashNeeded
            ? user
            : null;
    }
}
