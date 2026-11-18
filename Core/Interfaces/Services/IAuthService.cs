using WebMPS.Models;

namespace WebMPS.Core.Interfaces.Services;

public interface IAuthService
{
    AppUser? Authenticate(string username, string password);
    string HashPassword(AppUser user, string password);
}
