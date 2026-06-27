using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

/// <summary>
/// Base for simple Name+Description CRUD services that upsert by Name during import.
/// Subclasses provide the table/column metadata and Map/BindCreate/BindUpdate overrides.
/// </summary>
public abstract class NameDescServiceBase<T>
{
    protected readonly string _cs;
    protected NameDescServiceBase(string connectionString) => _cs = connectionString;

    protected abstract string Table   { get; }
    protected abstract string IdCol   { get; }
    protected abstract string OrderBy { get; }

    protected abstract T Map(SqlDataReader r);
    protected abstract void BindCreate(SqlCommand cmd, T item);
    protected abstract void BindUpdate(SqlCommand cmd, T item);

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<List<T>> GetAllAsync(CancellationToken ct = default)
    {
        var sql = $"SELECT * FROM [dbo].[{Table}] ORDER BY [{OrderBy}];";
        var list = new List<T>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        await using var r   = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(Map(r));
        return list;
    }

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var sql = $"SELECT * FROM [dbo].[{Table}] WHERE [{IdCol}]=@ID;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Map(r) : default;
    }

    public async Task<int> CreateAsync(T item, CancellationToken ct = default)
    {
        var sql = $"INSERT INTO [dbo].[{Table}]([Name],[Description]) OUTPUT INSERTED.[{IdCol}] VALUES(@Name,@Desc);";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        BindCreate(cmd, item);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateAsync(T item, CancellationToken ct = default)
    {
        var sql = $"UPDATE [dbo].[{Table}] SET [Name]=@Name,[Description]=@Desc WHERE [{IdCol}]=@ID;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        BindUpdate(cmd, item);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var sql = $"DELETE FROM [dbo].[{Table}] WHERE [{IdCol}]=@ID;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Import ────────────────────────────────────────────────────────────────

    public async Task<ImportResult> ImportAsync(Stream stream, string fileName,
        char delimiter = ',', CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        IAsyncEnumerable<Dictionary<string, string>> rows = ext is ".xlsx" or ".xls"
            ? ImportHelper.ExcelRowsAsync(ms)
            : ImportHelper.TextRowsAsync(ms, delimiter, ct);

        var result = new ImportResult();
        int rowNum = 1;
        await foreach (var row in rows.WithCancellation(ct))
        {
            rowNum++;
            string G(string k) => row.TryGetValue(k, out var v) ? v : string.Empty;
            var name = G("Name");
            if (string.IsNullOrWhiteSpace(name)) { result.RowsSkipped++; continue; }
            try   { await UpsertRowAsync(name, G("Description"), result, ct); }
            catch (Exception ex) { result.Errors.Add($"Row {rowNum}: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task UpsertRowAsync(string name, string description, ImportResult result, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var chk = new SqlCommand(
            $"SELECT [{IdCol}] FROM [dbo].[{Table}] WHERE [Name]=@Name;", conn);
        chk.Parameters.AddWithValue("@Name", name.Trim());
        var existing = await chk.ExecuteScalarAsync(ct);

        if (existing is int id)
        {
            await using var upd = new SqlCommand(
                $"UPDATE [dbo].[{Table}] SET [Description]=@Desc WHERE [{IdCol}]=@ID;", conn);
            upd.Parameters.AddWithValue("@ID",   id);
            upd.Parameters.AddWithValue("@Desc", (object?)ImportHelper.NullIfEmpty(description) ?? DBNull.Value);
            await upd.ExecuteNonQueryAsync(ct);
            result.AccountsUpdated++;
        }
        else
        {
            await using var ins = new SqlCommand(
                $"INSERT INTO [dbo].[{Table}]([Name],[Description]) VALUES(@Name,@Desc);", conn);
            ins.Parameters.AddWithValue("@Name", name.Trim());
            ins.Parameters.AddWithValue("@Desc", (object?)ImportHelper.NullIfEmpty(description) ?? DBNull.Value);
            await ins.ExecuteNonQueryAsync(ct);
            result.AccountsCreated++;
        }
    }
}
