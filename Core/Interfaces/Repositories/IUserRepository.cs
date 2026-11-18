using WebMPS.Models;

namespace WebMPS.Core.Interfaces.Repositories;

public interface IUserRepository
{
    AppUser? GetById(string id);
    AppUser? GetByUsername(string username);
    List<AppUser> GetAll();
    void Insert(AppUser user);
    void Update(AppUser user);
    bool UpdateStatus(string id, bool isActive);
    bool Delete(string id);
}
