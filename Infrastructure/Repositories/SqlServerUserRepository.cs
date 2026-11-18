using Dapper;
using WebMPS.Core.Interfaces.Repositories;
using WebMPS.Infrastructure.Data;
using WebMPS.Models;

namespace WebMPS.Infrastructure.Repositories;

public class SqlServerUserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SqlServerUserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public AppUser? GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        using var conn = _connectionFactory.CreateConnection();
        return conn.QueryFirstOrDefault<AppUser>(@"
            SELECT Id, Username, PasswordHash, DisplayName, Role, AssignedScope, IsActive, CreatedAt
            FROM Users
            WHERE Id = @Id;
        ", new { Id = id });
    }

    public AppUser? GetByUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        using var conn = _connectionFactory.CreateConnection();
        return conn.QueryFirstOrDefault<AppUser>(@"
            SELECT Id, Username, PasswordHash, DisplayName, Role, AssignedScope, IsActive, CreatedAt
            FROM Users
            WHERE LOWER(LTRIM(RTRIM(Username))) = LOWER(@Username);
        ", new { Username = username.Trim() });
    }

    public List<AppUser> GetAll()
    {
        using var conn = _connectionFactory.CreateConnection();
        return conn.Query<AppUser>(@"
            SELECT Id, Username, PasswordHash, DisplayName, Role, AssignedScope, IsActive, CreatedAt
            FROM Users
            ORDER BY Role, Username;
        ").ToList();
    }

    public void Insert(AppUser user)
    {
        using var conn = _connectionFactory.CreateConnection();
        conn.Execute(@"
            INSERT INTO Users (Id, Username, PasswordHash, DisplayName, Role, AssignedScope, IsActive, CreatedAt)
            VALUES (@Id, @Username, @PasswordHash, @DisplayName, @Role, @AssignedScope, @IsActive, @CreatedAt);
        ", new
        {
            user.Id,
            Username = user.Username.Trim(),
            user.PasswordHash,
            user.DisplayName,
            user.Role,
            user.AssignedScope,
            IsActive = user.IsActive ? 1 : 0,
            user.CreatedAt
        });
    }

    public void Update(AppUser user)
    {
        using var conn = _connectionFactory.CreateConnection();
        conn.Execute(@"
            UPDATE Users SET
                Username = @Username,
                PasswordHash = CASE WHEN @PasswordHash IS NOT NULL AND @PasswordHash <> '' THEN @PasswordHash ELSE PasswordHash END,
                DisplayName = @DisplayName,
                Role = @Role,
                AssignedScope = @AssignedScope,
                IsActive = @IsActive
            WHERE Id = @Id;
        ", new
        {
            user.Id,
            Username = user.Username.Trim(),
            user.PasswordHash,
            user.DisplayName,
            user.Role,
            user.AssignedScope,
            IsActive = user.IsActive ? 1 : 0
        });
    }

    public bool UpdateStatus(string id, bool isActive)
    {
        using var conn = _connectionFactory.CreateConnection();
        int affected = conn.Execute(
            "UPDATE Users SET IsActive = @IsActive WHERE Id = @Id;",
            new { Id = id, IsActive = isActive ? 1 : 0 });
        return affected > 0;
    }

    public bool Delete(string id)
    {
        using var conn = _connectionFactory.CreateConnection();
        int affected = conn.Execute("DELETE FROM Users WHERE Id = @Id;", new { Id = id });
        return affected > 0;
    }
}
