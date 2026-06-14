using Microsoft.Data.SqlClient;

namespace Portal.Services;

public sealed class SettingsService
{
    private readonly string _connectionString;

    public SettingsService(string connectionString) => _connectionString = connectionString;

    public async Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT [Name],[Value] FROM [dbo].[Settings] WHERE [IsEnabled]=1;";
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetString(0)] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        return result;
    }

    public async Task UpdateAsync(string name, string value, CancellationToken ct = default)
    {
        const string sql = """
            IF EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name]=@Name)
                UPDATE [dbo].[Settings] SET [Value]=@Value WHERE [Name]=@Name;
            ELSE
                INSERT INTO [dbo].[Settings] ([Name],[Value],[IsEnabled]) VALUES (@Name,@Value,1);
            """;
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Name",  name);
        cmd.Parameters.AddWithValue("@Value", value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public static string Require(IReadOnlyDictionary<string, string> s, string key) =>
        s.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v
        : throw new InvalidOperationException($"Required setting '{key}' is missing from [dbo].[Settings].");

    public static string GetOrDefault(IReadOnlyDictionary<string, string> s, string key, string def) =>
        s.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : def;
}
