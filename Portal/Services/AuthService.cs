using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

/// <summary>
/// Validates credentials and manages password hashing (PBKDF2 / SHA-256, 100 000 iterations).
/// </summary>
public sealed class AuthService
{
    private readonly string _connectionString;

    public AuthService(string connectionString) => _connectionString = connectionString;

    /// <summary>Returns the authenticated user or null if credentials are invalid.</summary>
    public async Task<User?> ValidateAsync(string username, string password, CancellationToken ct = default)
    {
        const string sql = """
            SELECT [UserID],[Username],[PasswordHash],[PasswordSalt],[FullName],[Email],
                   [RoleType],[IsActive],[CreatedAt],[LastLoginAt]
            FROM   [dbo].[Users]
            WHERE  [Username] = @Username AND [IsActive] = 1;
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Username", username);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct)) return null;

        var user = MapUser(reader);
        await reader.CloseAsync();

        if (!VerifyPassword(password, user.PasswordHash, user.PasswordSalt)) return null;

        // Update last login timestamp
        const string updateSql = "UPDATE [dbo].[Users] SET [LastLoginAt]=GETDATE() WHERE [UserID]=@UserID;";
        await using var upd = new SqlCommand(updateSql, conn);
        upd.Parameters.AddWithValue("@UserID", user.UserID);
        await upd.ExecuteNonQueryAsync(ct);

        return user;
    }

    public static string GenerateSalt()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public static string HashPassword(string password, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        using var pbkdf2 = new Rfc2898DeriveBytes(
            Encoding.UTF8.GetBytes(password), saltBytes, 100_000, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(pbkdf2.GetBytes(32));
    }

    public static bool VerifyPassword(string password, string hash, string salt)
    {
        var computed = HashPassword(password, salt);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(computed),
            Convert.FromBase64String(hash));
    }

    internal static User MapUser(SqlDataReader r) => new()
    {
        UserID       = r.GetInt32(0),
        Username     = r.GetString(1),
        PasswordHash = r.GetString(2),
        PasswordSalt = r.GetString(3),
        FullName     = r.GetString(4),
        Email        = r.IsDBNull(5) ? null : r.GetString(5),
        RoleType     = r.GetString(6),
        IsActive     = r.GetBoolean(7),
        CreatedAt    = r.GetDateTime(8),
        LastLoginAt  = r.IsDBNull(9) ? null : r.GetDateTime(9),
    };
}
