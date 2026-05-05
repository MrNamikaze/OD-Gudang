using System.Security.Claims;
using backend.Entities;
using BCrypt.Net;
using Npgsql;

namespace backend.Services;

public sealed class AuthService
{
    private readonly NpgsqlDataSource _dataSource;

    public AuthService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<ApplicationUser?> ValidateUserAsync(string username, string password)
    {
        var normalized = username.Trim().ToLowerInvariant();
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT id, username, email, full_name, password_hash, role, is_active, created_at, updated_at
            FROM users
            WHERE LOWER(username) = @username
            LIMIT 1;
            """, connection);
        command.Parameters.AddWithValue("username", normalized);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var user = new ApplicationUser
        {
            Id = reader.GetGuid(0),
            Username = reader.GetString(1),
            Email = reader.GetString(2),
            FullName = reader.GetString(3),
            PasswordHash = reader.GetString(4),
            Role = reader.GetString(5),
            IsActive = reader.GetBoolean(6),
            CreatedAt = reader.GetDateTime(7),
            UpdatedAt = reader.GetDateTime(8)
        };

        if (!user.IsActive)
        {
            return null;
        }

        var isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        return isValid ? user : null;
    }

    public static List<Claim> BuildClaims(ApplicationUser user)
    {
        return
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("full_name", user.FullName),
            new Claim(ClaimTypes.Role, user.Role)
        ];
    }
}
