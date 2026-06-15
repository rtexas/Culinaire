using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class EodSetupService
{
    private readonly string _cs;
    public EodSetupService(string connectionString) => _cs = connectionString;

    // ── Version list ──────────────────────────────────────────────────────────

    public async Task<List<EodSetup>> GetVersionsForLocationAsync(int locationID, CancellationToken ct = default)
    {
        const string sql = """
            SELECT s.[SetupID],s.[LocationID],s.[VersionNumber],ISNULL(s.[Description],''),
                   s.[IsEnabled],s.[CreatedAt],l.[Code],l.[Name]
            FROM   [dbo].[EodSetups] s
            JOIN   [dbo].[Locations] l ON l.[LocationID]=s.[LocationID]
            WHERE  s.[LocationID]=@LocID
            ORDER  BY s.[VersionNumber] DESC;
            """;
        var list = new List<EodSetup>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@LocID", locationID);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new EodSetup
            {
                SetupID       = r.GetInt32(0), LocationID    = r.GetInt32(1),
                VersionNumber = r.GetInt32(2), Description   = r.GetString(3),
                IsEnabled     = r.GetBoolean(4), CreatedAt   = r.GetDateTime(5),
                LocationCode  = r.GetString(6),  LocationName = r.GetString(7),
            });
        return list;
    }

    // ── Full layout for editor ────────────────────────────────────────────────

    public async Task<EodSetupLayout> GetLayoutAsync(int setupID, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        // Header
        var setup = await GetSetupHeaderAsync(setupID, conn, ct);

        // Columns
        var columns = new List<EodSetupColumnDef>();
        await using (var cmd = new SqlCommand("""
            SELECT sc.[SetupColumnID],sc.[ColumnID],c.[Name],ISNULL(c.[Description],''),
                   sc.[DisplayOrder],c.[CoaSegmentNumber],ISNULL(c.[SegmentValue],'')
            FROM   [dbo].[EodSetupColumns] sc
            JOIN   [dbo].[EodColumns] c ON c.[ColumnID]=sc.[ColumnID]
            WHERE  sc.[SetupID]=@ID
            ORDER  BY sc.[DisplayOrder],c.[Name];
            """, conn))
        {
            cmd.Parameters.AddWithValue("@ID", setupID);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                columns.Add(new EodSetupColumnDef
                {
                    SetupColumnID    = r.GetInt32(0), ColumnID         = r.GetInt32(1),
                    ColumnName       = r.GetString(2), ColumnDesc       = r.GetString(3),
                    DisplayOrder     = r.GetInt32(4),  CoaSegmentNumber = r.GetInt32(5),
                    SegmentValue     = r.GetString(6),
                });
        }

        // Rows
        var rows = new List<EodSetupRowDef>();
        await using (var cmd = new SqlCommand("""
            SELECT sr.[SetupRowID],sr.[RowID],ro.[Name],ISNULL(ro.[Description],''),
                   sr.[DisplayOrder],ro.[SectionID],ISNULL(sec.[Name],''),ISNULL(sec.[Multiplier],1),
                   ISNULL(sec.[UseInEodSales],1)
            FROM   [dbo].[EodSetupRows] sr
            JOIN   [dbo].[EodRows]     ro  ON ro.[RowID]      = sr.[RowID]
            LEFT JOIN [dbo].[EodSections] sec ON sec.[SectionID] = ro.[SectionID]
            WHERE  sr.[SetupID]=@ID
            ORDER  BY sr.[DisplayOrder],ro.[Name];
            """, conn))
        {
            cmd.Parameters.AddWithValue("@ID", setupID);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                rows.Add(new EodSetupRowDef
                {
                    SetupRowID    = r.GetInt32(0),  RowID        = r.GetInt32(1),
                    RowName       = r.GetString(2), RowDesc      = r.GetString(3),
                    DisplayOrder  = r.GetInt32(4),  SectionID    = r.GetInt32(5),
                    SectionName   = r.GetString(6), Multiplier   = r.GetInt32(7),
                    UseInEodSales = r.GetBoolean(8),
                });
        }

        // Cells
        var cells = new List<EodSetupCell>();
        await using (var cmd = new SqlCommand("""
            SELECT c.[CellID],c.[SetupID],c.[RowID],c.[ColumnID],c.[AccountID],
                   ISNULL(a.[FullAccountString],'')
            FROM   [dbo].[EodSetupCells] c
            LEFT JOIN [dbo].[ChartOfAccounts] a ON a.[AccountID]=c.[AccountID]
            WHERE  c.[SetupID]=@ID;
            """, conn))
        {
            cmd.Parameters.AddWithValue("@ID", setupID);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                cells.Add(new EodSetupCell
                {
                    CellID        = r.GetInt32(0), SetupID   = r.GetInt32(1),
                    RowID         = r.GetInt32(2), ColumnID  = r.GetInt32(3),
                    AccountID     = r.IsDBNull(4) ? null : r.GetInt32(4),
                    AccountString = r.GetString(5),
                });
        }

        return new EodSetupLayout { Setup = setup, Columns = columns, Rows = rows, Cells = cells };
    }

    // ── Create / Copy ─────────────────────────────────────────────────────────

    public async Task<EodSetup> CreateVersionAsync(int locationID, string description, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("""
            DECLARE @Next INT = (SELECT ISNULL(MAX([VersionNumber]),0)+1 FROM [dbo].[EodSetups] WHERE [LocationID]=@LocID);
            INSERT INTO [dbo].[EodSetups]([LocationID],[VersionNumber],[Description],[IsEnabled])
            VALUES(@LocID,@Next,@Desc, CASE WHEN @Next=1 THEN 1 ELSE 0 END);
            SELECT SCOPE_IDENTITY();
            """, conn);
        cmd.Parameters.AddWithValue("@LocID", locationID);
        cmd.Parameters.AddWithValue("@Desc",  (object?)NullIfEmpty(description) ?? DBNull.Value);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return await GetSetupHeaderAsync(id, conn, ct);
    }

    public async Task<EodSetup> CopyVersionAsync(int fromSetupID, string description, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        // Get source location
        await using var locCmd = new SqlCommand(
            "SELECT [LocationID] FROM [dbo].[EodSetups] WHERE [SetupID]=@ID;", conn);
        locCmd.Parameters.AddWithValue("@ID", fromSetupID);
        var locationID = (int)(await locCmd.ExecuteScalarAsync(ct))!;

        // Create new version
        await using var createCmd = new SqlCommand("""
            DECLARE @Next INT = (SELECT ISNULL(MAX([VersionNumber]),0)+1 FROM [dbo].[EodSetups] WHERE [LocationID]=@LocID);
            INSERT INTO [dbo].[EodSetups]([LocationID],[VersionNumber],[Description])
            VALUES(@LocID,@Next,@Desc);
            SELECT SCOPE_IDENTITY();
            """, conn);
        createCmd.Parameters.AddWithValue("@LocID", locationID);
        createCmd.Parameters.AddWithValue("@Desc",  (object?)NullIfEmpty(description) ?? DBNull.Value);
        var newID = Convert.ToInt32(await createCmd.ExecuteScalarAsync(ct));

        // Copy columns
        await using (var cmd = new SqlCommand("""
            INSERT INTO [dbo].[EodSetupColumns]([SetupID],[ColumnID],[DisplayOrder])
            SELECT @NewID,[ColumnID],[DisplayOrder] FROM [dbo].[EodSetupColumns] WHERE [SetupID]=@FromID;
            """, conn))
        {
            cmd.Parameters.AddWithValue("@NewID",  newID);
            cmd.Parameters.AddWithValue("@FromID", fromSetupID);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Copy rows
        await using (var cmd = new SqlCommand("""
            INSERT INTO [dbo].[EodSetupRows]([SetupID],[RowID],[DisplayOrder])
            SELECT @NewID,[RowID],[DisplayOrder] FROM [dbo].[EodSetupRows] WHERE [SetupID]=@FromID;
            """, conn))
        {
            cmd.Parameters.AddWithValue("@NewID",  newID);
            cmd.Parameters.AddWithValue("@FromID", fromSetupID);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Copy cells
        await using (var cmd = new SqlCommand("""
            INSERT INTO [dbo].[EodSetupCells]([SetupID],[RowID],[ColumnID],[AccountID])
            SELECT @NewID,[RowID],[ColumnID],[AccountID] FROM [dbo].[EodSetupCells] WHERE [SetupID]=@FromID;
            """, conn))
        {
            cmd.Parameters.AddWithValue("@NewID",  newID);
            cmd.Parameters.AddWithValue("@FromID", fromSetupID);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return await GetSetupHeaderAsync(newID, conn, ct);
    }

    // ── Save layout (replaces columns/rows/cells for the setup) ──────────────

    public async Task SaveLayoutAsync(
        int setupID,
        string description,
        IEnumerable<(int ColumnID, int Order)> columns,
        IEnumerable<(int RowID, int Order)> rows,
        IEnumerable<(int RowID, int ColumnID, int? AccountID)> cells,
        CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var txn = (SqlTransaction)await conn.BeginTransactionAsync(ct);
        try
        {
            // Update description
            await using (var cmd = new SqlCommand(
                "UPDATE [dbo].[EodSetups] SET [Description]=@Desc WHERE [SetupID]=@ID;", conn, txn))
            {
                cmd.Parameters.AddWithValue("@Desc", (object?)NullIfEmpty(description) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ID",   setupID);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Replace columns
            await ExecAsync("DELETE FROM [dbo].[EodSetupColumns] WHERE [SetupID]=@ID;", setupID, conn, txn, ct);
            foreach (var (colID, order) in columns)
                await using (var cmd = new SqlCommand(
                    "INSERT INTO [dbo].[EodSetupColumns]([SetupID],[ColumnID],[DisplayOrder]) VALUES(@S,@C,@O);", conn, txn))
                {
                    cmd.Parameters.AddWithValue("@S", setupID);
                    cmd.Parameters.AddWithValue("@C", colID);
                    cmd.Parameters.AddWithValue("@O", order);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

            // Replace rows
            await ExecAsync("DELETE FROM [dbo].[EodSetupRows] WHERE [SetupID]=@ID;", setupID, conn, txn, ct);
            foreach (var (rowID, order) in rows)
                await using (var cmd = new SqlCommand(
                    "INSERT INTO [dbo].[EodSetupRows]([SetupID],[RowID],[DisplayOrder]) VALUES(@S,@R,@O);", conn, txn))
                {
                    cmd.Parameters.AddWithValue("@S", setupID);
                    cmd.Parameters.AddWithValue("@R", rowID);
                    cmd.Parameters.AddWithValue("@O", order);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

            // Replace cells
            await ExecAsync("DELETE FROM [dbo].[EodSetupCells] WHERE [SetupID]=@ID;", setupID, conn, txn, ct);
            foreach (var (rowID, colID, accountID) in cells)
                await using (var cmd = new SqlCommand(
                    "INSERT INTO [dbo].[EodSetupCells]([SetupID],[RowID],[ColumnID],[AccountID]) VALUES(@S,@R,@C,@A);", conn, txn))
                {
                    cmd.Parameters.AddWithValue("@S", setupID);
                    cmd.Parameters.AddWithValue("@R", rowID);
                    cmd.Parameters.AddWithValue("@C", colID);
                    cmd.Parameters.AddWithValue("@A", (object?)accountID ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

            await txn.CommitAsync(ct);
        }
        catch { await txn.RollbackAsync(ct); throw; }
    }

    // ── Enable version (disables all others for the location) ─────────────────

    public async Task EnableVersionAsync(int setupID, int locationID, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var txn = (SqlTransaction)await conn.BeginTransactionAsync(ct);
        try
        {
            await using (var cmd = new SqlCommand(
                "UPDATE [dbo].[EodSetups] SET [IsEnabled]=0 WHERE [LocationID]=@LocID;", conn, txn))
            {
                cmd.Parameters.AddWithValue("@LocID", locationID);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await using (var cmd = new SqlCommand(
                "UPDATE [dbo].[EodSetups] SET [IsEnabled]=1 WHERE [SetupID]=@ID;", conn, txn))
            {
                cmd.Parameters.AddWithValue("@ID", setupID);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await txn.CommitAsync(ct);
        }
        catch { await txn.RollbackAsync(ct); throw; }
    }

    public async Task DisableVersionAsync(int setupID, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "UPDATE [dbo].[EodSetups] SET [IsEnabled]=0 WHERE [SetupID]=@ID;", conn);
        cmd.Parameters.AddWithValue("@ID", setupID);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteVersionAsync(int setupID, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var txn = (SqlTransaction)await conn.BeginTransactionAsync(ct);
        try
        {
            await ExecAsync("DELETE FROM [dbo].[EodSetupCells]   WHERE [SetupID]=@ID;", setupID, conn, txn, ct);
            await ExecAsync("DELETE FROM [dbo].[EodSetupColumns] WHERE [SetupID]=@ID;", setupID, conn, txn, ct);
            await ExecAsync("DELETE FROM [dbo].[EodSetupRows]    WHERE [SetupID]=@ID;", setupID, conn, txn, ct);
            await ExecAsync("DELETE FROM [dbo].[EodSetups]       WHERE [SetupID]=@ID;", setupID, conn, txn, ct);
            await txn.CommitAsync(ct);
        }
        catch { await txn.RollbackAsync(ct); throw; }
    }

    // ── Active setup for EOD Sales ────────────────────────────────────────────

    public async Task<bool> HasAnyVersionAsync(int locationID, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "SELECT COUNT(1) FROM [dbo].[EodSetups] WHERE [LocationID]=@LocID;", conn);
        cmd.Parameters.AddWithValue("@LocID", locationID);
        return (int)(await cmd.ExecuteScalarAsync(ct))! > 0;
    }

    public async Task<EodSetupLayout?> GetActiveLayoutForLocationAsync(int locationID, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "SELECT TOP 1 [SetupID] FROM [dbo].[EodSetups] WHERE [LocationID]=@LocID AND [IsEnabled]=1;", conn);
        cmd.Parameters.AddWithValue("@LocID", locationID);
        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is null) return null;
        return await GetLayoutAsync((int)result, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task ExecAsync(string sql, int id, SqlConnection conn, SqlTransaction txn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(sql, conn, txn);
        cmd.Parameters.AddWithValue("@ID", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<EodSetup> GetSetupHeaderAsync(int setupID, SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("""
            SELECT s.[SetupID],s.[LocationID],s.[VersionNumber],ISNULL(s.[Description],''),
                   s.[IsEnabled],s.[CreatedAt],l.[Code],l.[Name]
            FROM   [dbo].[EodSetups] s
            JOIN   [dbo].[Locations] l ON l.[LocationID]=s.[LocationID]
            WHERE  s.[SetupID]=@ID;
            """, conn);
        cmd.Parameters.AddWithValue("@ID", setupID);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        return new EodSetup
        {
            SetupID       = r.GetInt32(0), LocationID    = r.GetInt32(1),
            VersionNumber = r.GetInt32(2), Description   = r.GetString(3),
            IsEnabled     = r.GetBoolean(4), CreatedAt   = r.GetDateTime(5),
            LocationCode  = r.GetString(6),  LocationName = r.GetString(7),
        };
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
