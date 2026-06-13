using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class CountryService
{
    private readonly string _cs;
    public CountryService(string connectionString) => _cs = connectionString;

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<List<Country>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT [CountryID],[Name],[Code],[Description],[IsActive],[CreatedAt] FROM [dbo].[Countries] ORDER BY [Name];";
        var list = new List<Country>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Map(reader));
        return list;
    }

    public async Task<int> CreateAsync(Country item, CancellationToken ct = default)
    {
        const string sql = "INSERT INTO [dbo].[Countries]([Name],[Code],[Description]) OUTPUT INSERTED.[CountryID] VALUES(@Name,@Code,@Desc);";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Name", item.Name.Trim());
        cmd.Parameters.AddWithValue("@Code", item.Code.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@Desc", (object?)Null(item.Description) ?? DBNull.Value);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateAsync(Country item, CancellationToken ct = default)
    {
        const string sql = "UPDATE [dbo].[Countries] SET [Name]=@Name,[Code]=@Code,[Description]=@Desc WHERE [CountryID]=@ID;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID",   item.CountryID);
        cmd.Parameters.AddWithValue("@Name", item.Name.Trim());
        cmd.Parameters.AddWithValue("@Code", item.Code.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@Desc", (object?)Null(item.Description) ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM [dbo].[Countries] WHERE [CountryID]=@ID;";
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
        var headers = BuildHeaderMap(ws);
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (int row = 2; row <= lastRow; row++)
        {
            string Get(string key) => headers.TryGetValue(key, out var col) ? ws.Cell(row, col).GetString().Trim() : string.Empty;
            try   { await UpsertAsync(Get("Name"), Get("Code"), Get("Description"), result, ct); }
            catch (Exception ex) { result.Errors.Add($"Row {row}: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task<ImportResult> ImportTextAsync(Stream stream, char delimiter, CancellationToken ct)
    {
        var result  = new ImportResult();
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        var headerLine   = await reader.ReadLineAsync(ct);
        if (headerLine is null) return result;

        var headers = headerLine.Split(delimiter)
            .Select((h, i) => (h.Trim(), i))
            .Where(x => !string.IsNullOrEmpty(x.Item1))
            .ToDictionary(x => x.Item1, x => x.i, StringComparer.OrdinalIgnoreCase);

        int lineNum = 1;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNum++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ParseLine(line, delimiter);
            string Get(string key) => headers.TryGetValue(key, out var idx) && idx < cols.Length ? cols[idx].Trim() : string.Empty;
            try   { await UpsertAsync(Get("Name"), Get("Code"), Get("Description"), result, ct); }
            catch (Exception ex) { result.Errors.Add($"Line {lineNum}: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task UpsertAsync(string name, string code, string description, ImportResult result, CancellationToken ct)
    {
        code = code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) { result.Errors.Add($"Skipped '{name}' — Code is required."); result.RowsSkipped++; return; }

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var checkCmd = new SqlCommand("SELECT [CountryID] FROM [dbo].[Countries] WHERE [Code]=@Code;", conn);
        checkCmd.Parameters.AddWithValue("@Code", code);
        var existing = await checkCmd.ExecuteScalarAsync(ct);

        if (existing is int id)
        {
            await using var upd = new SqlCommand("UPDATE [dbo].[Countries] SET [Name]=@Name,[Description]=@Desc WHERE [CountryID]=@ID;", conn);
            upd.Parameters.AddWithValue("@ID",   id);
            upd.Parameters.AddWithValue("@Name", name);
            upd.Parameters.AddWithValue("@Desc", (object?)Null(description) ?? DBNull.Value);
            await upd.ExecuteNonQueryAsync(ct);
            result.AccountsUpdated++;
        }
        else
        {
            await using var ins = new SqlCommand("INSERT INTO [dbo].[Countries]([Name],[Code],[Description]) VALUES(@Name,@Code,@Desc);", conn);
            ins.Parameters.AddWithValue("@Name", name);
            ins.Parameters.AddWithValue("@Code", code);
            ins.Parameters.AddWithValue("@Desc", (object?)Null(description) ?? DBNull.Value);
            await ins.ExecuteNonQueryAsync(ct);
            result.AccountsCreated++;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet ws)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (int c = 1; c <= lastCol; c++)
        {
            var h = ws.Cell(1, c).GetString().Trim();
            if (!string.IsNullOrEmpty(h)) map[h] = c;
        }
        return map;
    }

    private static string[] ParseLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool inQ = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"') { if (inQ && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else inQ = !inQ; }
            else if (c == delimiter && !inQ) { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return [.. fields];
    }

    private static string? Null(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static Country Map(SqlDataReader r) => new()
    {
        CountryID   = r.GetInt32(0),
        Name        = r.GetString(1),
        Code        = r.GetString(2),
        Description = r.IsDBNull(3) ? string.Empty : r.GetString(3),
        IsActive    = r.GetBoolean(4),
        CreatedAt   = r.GetDateTime(5),
    };
}
