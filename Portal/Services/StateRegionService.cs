using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class StateRegionService
{
    private readonly string _cs;
    public StateRegionService(string connectionString) => _cs = connectionString;

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<List<StateRegion>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT [StateRegionID],[Name],[Code],[Description],[IsActive],[CreatedAt] FROM [dbo].[StatesRegions] ORDER BY [Name];";
        var list = new List<StateRegion>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Map(reader));
        return list;
    }

    public async Task<int> CreateAsync(StateRegion item, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [dbo].[StatesRegions]([Name],[Code],[Description])
            OUTPUT INSERTED.[StateRegionID]
            VALUES(@Name,@Code,@Desc);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Name", item.Name.Trim());
        cmd.Parameters.AddWithValue("@Code", item.Code.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@Desc", (object?)ImportHelper.NullIfEmpty(item.Description) ?? DBNull.Value);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateAsync(StateRegion item, CancellationToken ct = default)
    {
        const string sql = "UPDATE [dbo].[StatesRegions] SET [Name]=@Name,[Code]=@Code,[Description]=@Desc WHERE [StateRegionID]=@ID;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID",   item.StateRegionID);
        cmd.Parameters.AddWithValue("@Name", item.Name.Trim());
        cmd.Parameters.AddWithValue("@Code", item.Code.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@Desc", (object?)ImportHelper.NullIfEmpty(item.Description) ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM [dbo].[StatesRegions] WHERE [StateRegionID]=@ID;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Import ────────────────────────────────────────────────────────────────

    public async Task<ImportResult> ImportAsync(Stream stream, string fileName, char delimiter = ',', CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".xlsx" or ".xls"
            ? await ImportExcelAsync(ms, ct)
            : await ImportTextAsync(ms, delimiter, ct);
    }

    private async Task<ImportResult> ImportExcelAsync(Stream stream, CancellationToken ct)
    {
        var result = new ImportResult();
        using var wb = new XLWorkbook(stream);
        var ws      = wb.Worksheets.First();

        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (int c = 1; c <= lastCol; c++)
        {
            var h = ws.Cell(1, c).GetString().Trim();
            if (!string.IsNullOrEmpty(h)) headers[h] = c;
        }

        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (int row = 2; row <= lastRow; row++)
        {
            string Get(string key) => headers.TryGetValue(key, out var col) ? ws.Cell(row, col).GetString().Trim() : string.Empty;
            try   { await UpsertRowAsync(Get("Name"), Get("Code"), Get("Description"), result, ct); }
            catch (Exception ex) { result.Errors.Add($"Row {row}: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task<ImportResult> ImportTextAsync(Stream stream, char delimiter, CancellationToken ct)
    {
        var result = new ImportResult();
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);

        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine is null) return result;

        var headers = ImportHelper.ParseLine(headerLine, delimiter)
            .Select((h, i) => (h.Trim(), i))
            .Where(x => !string.IsNullOrEmpty(x.Item1))
            .ToDictionary(x => x.Item1, x => x.i, StringComparer.OrdinalIgnoreCase);

        int lineNum = 1;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNum++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ImportHelper.ParseLine(line, delimiter);
            string Get(string key) => headers.TryGetValue(key, out var idx) && idx < cols.Length ? cols[idx].Trim() : string.Empty;
            try   { await UpsertRowAsync(Get("Name"), Get("Code"), Get("Description"), result, ct); }
            catch (Exception ex) { result.Errors.Add($"Line {lineNum}: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task UpsertRowAsync(string name, string code, string description, ImportResult result, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(code)) { result.RowsSkipped++; return; }

        code = code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) { result.Errors.Add($"Row for '{name}' skipped — Code is required."); result.RowsSkipped++; return; }

        const string checkSql = "SELECT [StateRegionID] FROM [dbo].[StatesRegions] WHERE [Code]=@Code;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var checkCmd = new SqlCommand(checkSql, conn);
        checkCmd.Parameters.AddWithValue("@Code", code);
        var existing = await checkCmd.ExecuteScalarAsync(ct);

        if (existing is int existingId)
        {
            const string upd = "UPDATE [dbo].[StatesRegions] SET [Name]=@Name,[Description]=@Desc WHERE [StateRegionID]=@ID;";
            await using var updCmd = new SqlCommand(upd, conn);
            updCmd.Parameters.AddWithValue("@ID",   existingId);
            updCmd.Parameters.AddWithValue("@Name", name);
            updCmd.Parameters.AddWithValue("@Desc", (object?)ImportHelper.NullIfEmpty(description) ?? DBNull.Value);
            await updCmd.ExecuteNonQueryAsync(ct);
            result.AccountsUpdated++;
        }
        else
        {
            const string ins = "INSERT INTO [dbo].[StatesRegions]([Name],[Code],[Description]) VALUES(@Name,@Code,@Desc);";
            await using var insCmd = new SqlCommand(ins, conn);
            insCmd.Parameters.AddWithValue("@Name", name);
            insCmd.Parameters.AddWithValue("@Code", code);
            insCmd.Parameters.AddWithValue("@Desc", (object?)ImportHelper.NullIfEmpty(description) ?? DBNull.Value);
            await insCmd.ExecuteNonQueryAsync(ct);
            result.AccountsCreated++;
        }
    }

    private static StateRegion Map(SqlDataReader r) => new()
    {
        StateRegionID = r.GetInt32(0),
        Name         = r.GetString(1),
        Code         = r.GetString(2),
        Description  = r.IsDBNull(3) ? string.Empty : r.GetString(3),
        IsActive     = r.GetBoolean(4),
        CreatedAt    = r.GetDateTime(5),
    };
}
