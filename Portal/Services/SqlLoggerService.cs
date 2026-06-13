using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Portal.Services;

public sealed class SqlLoggerService
{
    private readonly string             _connectionString;
    private readonly LogLevel           _minLevel;
    private readonly FileLoggerService? _fileLogger;

    public SqlLoggerService(string connectionString, LogLevel minLevel = LogLevel.Warning, FileLoggerService? fileLogger = null)
    {
        _connectionString = connectionString;
        _minLevel         = minLevel;
        _fileLogger       = fileLogger;
    }

    public async Task LogAsync(string message, LogLevel logLevel = LogLevel.Information, CancellationToken ct = default)
    {
        _fileLogger?.WriteEntry(logLevel, message);
        if (logLevel >= _minLevel)
            await WriteSqlAsync(message, logLevel, ct);
    }

    public async Task PurgeOldLogsAsync(CancellationToken ct = default)
    {
        const string sql = "DELETE FROM [dbo].[Logging] WHERE [LoggedAt] < DATEADD(YEAR, -1, GETDATE());";
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);
            var deleted = await cmd.ExecuteNonQueryAsync(ct);
            _fileLogger?.WriteEntry(LogLevel.Information, $"PurgeOldLogs: {deleted} row(s) removed.");
        }
        catch { }
    }

    private async Task WriteSqlAsync(string message, LogLevel logLevel, CancellationToken ct)
    {
        const string sql = "INSERT INTO [dbo].[Logging] ([Message],[LogLevel]) VALUES (@Message,@LogLevel);";
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Message",  message);
            cmd.Parameters.AddWithValue("@LogLevel", (int)logLevel);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch { }
    }
}
