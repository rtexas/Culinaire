using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class PermissionService
{
    private readonly string _connectionString;

    public PermissionService(string connectionString) => _connectionString = connectionString;

    public async Task<List<Module>> GetAllModulesAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT [ModuleID],[ModuleName],[DisplayName],[RouteUrl],[IconClass],[SortOrder],[IsActive] FROM [dbo].[Modules] WHERE [IsActive]=1 ORDER BY [SortOrder];";
        var list = new List<Module>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(new Module
            {
                ModuleID    = reader.GetInt32(0),
                ModuleName  = reader.GetString(1),
                DisplayName = reader.GetString(2),
                RouteUrl    = reader.GetString(3),
                IconClass   = reader.IsDBNull(4) ? null : reader.GetString(4),
                SortOrder   = reader.GetInt32(5),
                IsActive    = reader.GetBoolean(6),
            });
        return list;
    }

    /// <summary>Returns the permission level for a specific user+module. Administrators always get ReadWrite.</summary>
    public async Task<string> GetLevelAsync(int userId, string roleType, string moduleName, CancellationToken ct = default)
    {
        if (roleType == "Administrator") return "ReadWrite";
        if (roleType == "Viewer")        return "Read";

        const string sql = """
            SELECT p.[PermissionLevel]
            FROM   [dbo].[UserModulePermissions] p
            JOIN   [dbo].[Modules] m ON m.[ModuleID] = p.[ModuleID]
            WHERE  p.[UserID] = @UserID AND m.[ModuleName] = @ModuleName;
            """;
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserID",     userId);
        cmd.Parameters.AddWithValue("@ModuleName", moduleName);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string s ? s : "None";
    }

    /// <summary>Returns all module permissions for a given user (for the Admin permissions editor).</summary>
    public async Task<List<UserModulePermission>> GetUserPermissionsAsync(int userId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.[PermissionID], p.[UserID], m.[ModuleID], p.[PermissionLevel],
                   m.[DisplayName], u.[Username]
            FROM   [dbo].[Modules] m
            LEFT JOIN [dbo].[UserModulePermissions] p
                   ON p.[ModuleID] = m.[ModuleID] AND p.[UserID] = @UserID
            LEFT JOIN [dbo].[Users] u ON u.[UserID] = p.[UserID]
            WHERE  m.[IsActive] = 1
            ORDER  BY m.[SortOrder];
            """;
        var list = new List<UserModulePermission>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserID", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(new UserModulePermission
            {
                PermissionID       = reader.IsDBNull(0) ? 0        : reader.GetInt32(0),
                UserID             = reader.IsDBNull(1) ? userId    : reader.GetInt32(1),
                ModuleID           = reader.GetInt32(2),
                PermissionLevel    = reader.IsDBNull(3) ? "None"    : reader.GetString(3),
                ModuleDisplayName  = reader.GetString(4),
                Username           = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            });
        return list;
    }

    public async Task UpsertPermissionAsync(int userId, int moduleId, string level, CancellationToken ct = default)
    {
        const string sql = """
            IF EXISTS (SELECT 1 FROM [dbo].[UserModulePermissions] WHERE [UserID]=@UserID AND [ModuleID]=@ModuleID)
                UPDATE [dbo].[UserModulePermissions] SET [PermissionLevel]=@Level WHERE [UserID]=@UserID AND [ModuleID]=@ModuleID;
            ELSE
                INSERT INTO [dbo].[UserModulePermissions] ([UserID],[ModuleID],[PermissionLevel]) VALUES (@UserID,@ModuleID,@Level);
            """;
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserID",   userId);
        cmd.Parameters.AddWithValue("@ModuleID", moduleId);
        cmd.Parameters.AddWithValue("@Level",    level);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Returns modules the user has at least Read access to (used to build the nav menu).</summary>
    public async Task<List<(Module Module, string Level)>> GetAccessibleModulesAsync(int userId, string roleType, CancellationToken ct = default)
    {
        var modules = await GetAllModulesAsync(ct);
        var result  = new List<(Module, string)>();
        foreach (var m in modules)
        {
            var level = await GetLevelAsync(userId, roleType, m.ModuleName, ct);
            if (level != "None") result.Add((m, level));
        }
        return result;
    }
}
