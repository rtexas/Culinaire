using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class EodSalesService
{
    private readonly string _cs;
    private readonly EodSetupService _setupService;

    public EodSalesService(string connectionString, EodSetupService setupService)
    {
        _cs           = connectionString;
        _setupService = setupService;
    }

    // ── Get or create entry for active setup ──────────────────────────────────

    public async Task<(EodSalesGrid? Grid, string? Error)> GetOrCreateAsync(
        DateOnly entryDate, int locationID, CancellationToken ct = default)
    {
        var layout = await _setupService.GetActiveLayoutForLocationAsync(locationID, ct);
        if (layout is null)
        {
            var hasAny = await _setupService.HasAnyVersionAsync(locationID, ct);
            var msg = hasAny
                ? "An EOD setup exists for this location but none is enabled. Go to Admin → EOD Sales Setup and click Enable on the version you want to use."
                : "No EOD setup found for this location. Go to Admin → EOD Sales Setup to create one.";
            return (null, msg);
        }

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        // Find or create the entry
        await using var findCmd = new SqlCommand(
            "SELECT [EntryID] FROM [dbo].[EodSalesEntries] WHERE [EntryDate]=@D AND [LocationID]=@L AND [SetupID]=@S;", conn);
        findCmd.Parameters.AddWithValue("@D", entryDate.ToDateTime(TimeOnly.MinValue));
        findCmd.Parameters.AddWithValue("@L", locationID);
        findCmd.Parameters.AddWithValue("@S", layout.Setup.SetupID);
        var existing = await findCmd.ExecuteScalarAsync(ct);

        int entryID;
        if (existing is int eid)
        {
            entryID = eid;
        }
        else
        {
            await using var ins = new SqlCommand("""
                INSERT INTO [dbo].[EodSalesEntries]([EntryDate],[LocationID],[SetupID])
                OUTPUT INSERTED.[EntryID]
                VALUES(@D,@L,@S);
                """, conn);
            ins.Parameters.AddWithValue("@D", entryDate.ToDateTime(TimeOnly.MinValue));
            ins.Parameters.AddWithValue("@L", locationID);
            ins.Parameters.AddWithValue("@S", layout.Setup.SetupID);
            entryID = (int)(await ins.ExecuteScalarAsync(ct))!;
        }

        return (await BuildGridAsync(entryID, layout, conn, ct), null);
    }

    // ── Load existing entry by ID ─────────────────────────────────────────────

    public async Task<EodSalesGrid?> GetGridAsync(int entryID, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var hdrCmd = new SqlCommand(
            "SELECT [SetupID] FROM [dbo].[EodSalesEntries] WHERE [EntryID]=@ID;", conn);
        hdrCmd.Parameters.AddWithValue("@ID", entryID);
        var setupID = await hdrCmd.ExecuteScalarAsync(ct);
        if (setupID is null) return null;
        var layout = await _setupService.GetLayoutAsync((int)setupID, ct);
        return await BuildGridAsync(entryID, layout, conn, ct);
    }

    // ── List entries for a location ───────────────────────────────────────────

    public async Task<List<EodSalesEntry>> GetEntriesForLocationAsync(int locationID, CancellationToken ct = default)
    {
        const string sql = """
            SELECT [EntryID],[EntryDate],[LocationID],[SetupID],
                   [IsSubmitted],[SubmittedAt],[IsVoided],[VoidedAt],[CreatedAt],[UpdatedAt]
            FROM   [dbo].[EodSalesEntries]
            WHERE  [LocationID]=@LocID
            ORDER  BY [EntryDate] DESC;
            """;
        var list = new List<EodSalesEntry>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@LocID", locationID);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(MapEntry(r));
        return list;
    }

    public async Task<List<EodSalesEntry>> GetRecentAsync(int locationID, int top = 20, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP(@Top) [EntryID],[EntryDate],[LocationID],[SetupID],
                   [IsSubmitted],[SubmittedAt],[IsVoided],[VoidedAt],[CreatedAt],[UpdatedAt]
            FROM   [dbo].[EodSalesEntries]
            WHERE  [LocationID]=@LocID
            ORDER  BY [EntryDate] DESC;
            """;
        var list = new List<EodSalesEntry>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@LocID", locationID);
        cmd.Parameters.AddWithValue("@Top",   top);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(MapEntry(r));
        return list;
    }

    public async Task SubmitAsync(int entryID, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "UPDATE [dbo].[EodSalesEntries] SET [IsSubmitted]=1,[SubmittedAt]=GETDATE(),[UpdatedAt]=GETDATE() WHERE [EntryID]=@ID AND [IsVoided]=0;",
            conn);
        cmd.Parameters.AddWithValue("@ID", entryID);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task VoidAsync(int entryID, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "UPDATE [dbo].[EodSalesEntries] SET [IsVoided]=1,[VoidedAt]=GETDATE(),[UpdatedAt]=GETDATE() WHERE [EntryID]=@ID AND [IsSubmitted]=0;",
            conn);
        cmd.Parameters.AddWithValue("@ID", entryID);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Save cell values ──────────────────────────────────────────────────────

    public async Task SaveValuesAsync(
        int entryID,
        IEnumerable<(int RowID, int ColumnID, decimal Amount)> values,
        CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        // Verify entry is editable
        await using var chk = new SqlCommand(
            "SELECT [IsSubmitted],[IsVoided] FROM [dbo].[EodSalesEntries] WHERE [EntryID]=@ID;", conn);
        chk.Parameters.AddWithValue("@ID", entryID);
        await using var chkR = await chk.ExecuteReaderAsync(ct);
        if (await chkR.ReadAsync(ct))
        {
            if (chkR.GetBoolean(0)) throw new InvalidOperationException("This entry has been submitted and cannot be modified.");
            if (chkR.GetBoolean(1)) throw new InvalidOperationException("This entry has been voided and cannot be modified.");
        }
        await chkR.CloseAsync();

        await using var txn = (SqlTransaction)await conn.BeginTransactionAsync(ct);
        try
        {
            foreach (var (rowID, colID, amount) in values)
            {
                await using var cmd = new SqlCommand("""
                    MERGE [dbo].[EodSalesValues] AS t
                    USING (VALUES(@E,@R,@C,@A)) AS s([EntryID],[RowID],[ColumnID],[Amount])
                        ON t.[EntryID]=s.[EntryID] AND t.[RowID]=s.[RowID] AND t.[ColumnID]=s.[ColumnID]
                    WHEN MATCHED    THEN UPDATE SET t.[Amount]=s.[Amount]
                    WHEN NOT MATCHED THEN INSERT([EntryID],[RowID],[ColumnID],[Amount]) VALUES(s.[EntryID],s.[RowID],s.[ColumnID],s.[Amount]);
                    """, conn, txn);
                cmd.Parameters.AddWithValue("@E", entryID);
                cmd.Parameters.AddWithValue("@R", rowID);
                cmd.Parameters.AddWithValue("@C", colID);
                cmd.Parameters.AddWithValue("@A", amount);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await using (var upd = new SqlCommand(
                "UPDATE [dbo].[EodSalesEntries] SET [UpdatedAt]=GETDATE() WHERE [EntryID]=@ID;", conn, txn))
            {
                upd.Parameters.AddWithValue("@ID", entryID);
                await upd.ExecuteNonQueryAsync(ct);
            }

            await txn.CommitAsync(ct);
        }
        catch { await txn.RollbackAsync(ct); throw; }
    }

    // ── Build grid DTO ────────────────────────────────────────────────────────

    private static async Task<EodSalesGrid> BuildGridAsync(
        int entryID, EodSetupLayout layout, SqlConnection conn, CancellationToken ct)
    {
        // Load entry header
        EodSalesEntry entry;
        await using (var cmd = new SqlCommand("""
            SELECT [EntryID],[EntryDate],[LocationID],[SetupID],
                   [IsSubmitted],[SubmittedAt],[IsVoided],[VoidedAt],[CreatedAt],[UpdatedAt]
            FROM   [dbo].[EodSalesEntries]
            WHERE  [EntryID]=@ID;
            """, conn))
        {
            cmd.Parameters.AddWithValue("@ID", entryID);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            await r.ReadAsync(ct);
            entry = MapEntry(r);
        }

        // Load values
        var valMap = new Dictionary<(int R, int C), decimal>();
        await using (var cmd = new SqlCommand(
            "SELECT [RowID],[ColumnID],[Amount] FROM [dbo].[EodSalesValues] WHERE [EntryID]=@ID;", conn))
        {
            cmd.Parameters.AddWithValue("@ID", entryID);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                valMap[(r.GetInt32(0), r.GetInt32(1))] = r.GetDecimal(2);
        }

        // Group rows by section (preserving layout display order)
        var sections = layout.Rows
            .GroupBy(row => (row.SectionID, row.SectionName, row.Multiplier))
            .OrderBy(g => layout.Rows.FindIndex(r => r.SectionID == g.Key.SectionID))
            .Select(g => new EodSalesGridSection
            {
                SectionID   = g.Key.SectionID,
                SectionName = g.Key.SectionName,
                Multiplier  = g.Key.Multiplier,
                Rows        = [.. g],
            })
            .ToList();

        return new EodSalesGrid
        {
            Entry    = entry,
            Layout   = layout,
            Sections = sections,
            Values   = valMap.ToDictionary(kv => (kv.Key.R, kv.Key.C), kv => kv.Value),
        };
    }

    private static EodSalesEntry MapEntry(SqlDataReader r) => new()
    {
        EntryID     = r.GetInt32(0),
        EntryDate   = r.GetDateTime(1),
        LocationID  = r.GetInt32(2),
        SetupID     = r.GetInt32(3),
        IsSubmitted = r.GetBoolean(4),
        SubmittedAt = r.IsDBNull(5) ? null : r.GetDateTime(5),
        IsVoided    = r.GetBoolean(6),
        VoidedAt    = r.IsDBNull(7) ? null : r.GetDateTime(7),
        CreatedAt   = r.GetDateTime(8),
        UpdatedAt   = r.GetDateTime(9),
    };
}
