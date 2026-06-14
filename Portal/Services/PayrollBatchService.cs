using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class PayrollBatchService
{
    private readonly string _cs;

    public PayrollBatchService(string connectionString) => _cs = connectionString;

    public async Task<List<PayrollBatch>> GetForLocationAsync(int locationId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT b.[BatchID],b.[LocationID],ISNULL(l.[Name],''),
                   b.[BatchNameTemplate],b.[PayPeriodLength],
                   b.[StartDayOfWeek],b.[IsActive],b.[CreatedAt]
            FROM   [dbo].[PayrollBatches] b
            JOIN   [dbo].[Locations]      l ON l.[LocationID]=b.[LocationID]
            WHERE  b.[LocationID]=@LocID
            ORDER  BY b.[BatchID];
            """;
        var list = new List<PayrollBatch>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@LocID", locationId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(Map(r));
        return list;
    }

    public async Task<int> CreateAsync(PayrollBatch b, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [dbo].[PayrollBatches]
                ([LocationID],[BatchNameTemplate],[PayPeriodLength],[StartDayOfWeek],[IsActive])
            OUTPUT INSERTED.[BatchID]
            VALUES(@LocID,@Template,@PeriodLen,@StartDay,@Active);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        AddParams(cmd, b);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateAsync(PayrollBatch b, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[PayrollBatches]
            SET [LocationID]=@LocID,[BatchNameTemplate]=@Template,
                [PayPeriodLength]=@PeriodLen,[StartDayOfWeek]=@StartDay,[IsActive]=@Active
            WHERE [BatchID]=@ID;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", b.BatchID);
        AddParams(cmd, b);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(int batchId, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM [dbo].[PayrollBatches] WHERE [BatchID]=@ID;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", batchId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static PayrollBatch Map(SqlDataReader r) => new()
    {
        BatchID           = r.GetInt32(0),
        LocationID        = r.GetInt32(1),
        LocationName      = r.GetString(2),
        BatchNameTemplate = r.GetString(3),
        PayPeriodLength   = r.GetString(4),
        StartDayOfWeek    = r.IsDBNull(5) ? null : r.GetString(5),
        IsActive          = r.GetBoolean(6),
        CreatedAt         = r.GetDateTime(7),
    };

    private static void AddParams(SqlCommand cmd, PayrollBatch b)
    {
        cmd.Parameters.AddWithValue("@LocID",     b.LocationID);
        cmd.Parameters.AddWithValue("@Template",  b.BatchNameTemplate.Trim());
        cmd.Parameters.AddWithValue("@PeriodLen", b.PayPeriodLength);
        cmd.Parameters.AddWithValue("@StartDay",  (object?)b.StartDayOfWeek ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Active",    b.IsActive);
    }
}
