using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class AccountTypeService
{
    private readonly string _cs;
    public AccountTypeService(string connectionString) => _cs = connectionString;

    public async Task<List<AccountType>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT [TypeID],[Name],[Description],[IsActive],[CreatedAt] FROM [dbo].[AccountTypes] ORDER BY [Name];";
        var list = new List<AccountType>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Map(reader));
        return list;
    }

    public async Task<AccountType?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        const string sql = "SELECT [TypeID],[Name],[Description],[IsActive],[CreatedAt] FROM [dbo].[AccountTypes] WHERE [TypeID]=@ID;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    /// <summary>Gets existing type by name, or creates it. Returns the ID.</summary>
    public async Task<int> EnsureAsync(string name, CancellationToken ct = default)
    {
        name = name.Trim();
        const string getSql = "SELECT [TypeID] FROM [dbo].[AccountTypes] WHERE [Name]=@Name;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var getCmd = new SqlCommand(getSql, conn);
        getCmd.Parameters.AddWithValue("@Name", name);
        var existing = await getCmd.ExecuteScalarAsync(ct);
        if (existing is int id) return id;

        const string insSql = "INSERT INTO [dbo].[AccountTypes]([Name],[Description]) OUTPUT INSERTED.[TypeID] VALUES(@Name,'');";
        await using var insCmd = new SqlCommand(insSql, conn);
        insCmd.Parameters.AddWithValue("@Name", name);
        return (int)(await insCmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> CreateAsync(AccountType item, CancellationToken ct = default)
    {
        const string sql = "INSERT INTO [dbo].[AccountTypes]([Name],[Description]) OUTPUT INSERTED.[TypeID] VALUES(@Name,@Desc);";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Name", item.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc", (object?)item.Description ?? DBNull.Value);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateAsync(AccountType item, CancellationToken ct = default)
    {
        const string sql = "UPDATE [dbo].[AccountTypes] SET [Name]=@Name,[Description]=@Desc WHERE [TypeID]=@ID;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID",   item.TypeID);
        cmd.Parameters.AddWithValue("@Name", item.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc", (object?)item.Description ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM [dbo].[AccountTypes] WHERE [TypeID]=@ID;";
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

    private async Task<ImportResult> ImportExcelAsync(Stream ms, CancellationToken ct)
    {
        var result = new ImportResult();
        using var wb = new XLWorkbook(ms);
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
            try   { await UpsertRowAsync(Get("Name"), Get("Description"), result, ct); }
            catch (Exception ex) { result.Errors.Add($"Row {row}: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task<ImportResult> ImportTextAsync(Stream ms, char delimiter, CancellationToken ct)
    {
        var result = new ImportResult();
        using var reader = new StreamReader(ms, detectEncodingFromByteOrderMarks: true);
        var headerLine   = await reader.ReadLineAsync(ct);
        if (headerLine is null) return result;

        var headers = ParseLine(headerLine, delimiter)
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
            try   { await UpsertRowAsync(Get("Name"), Get("Description"), result, ct); }
            catch (Exception ex) { result.Errors.Add($"Line {lineNum}: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task UpsertRowAsync(string name, string description, ImportResult result, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) { result.RowsSkipped++; return; }

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var chk = new SqlCommand("SELECT [TypeID] FROM [dbo].[AccountTypes] WHERE [Name]=@Name;", conn);
        chk.Parameters.AddWithValue("@Name", name.Trim());
        var existing = await chk.ExecuteScalarAsync(ct);

        if (existing is int id)
        {
            await using var upd = new SqlCommand("UPDATE [dbo].[AccountTypes] SET [Description]=@Desc WHERE [TypeID]=@ID;", conn);
            upd.Parameters.AddWithValue("@ID",   id);
            upd.Parameters.AddWithValue("@Desc", (object?)Null(description) ?? DBNull.Value);
            await upd.ExecuteNonQueryAsync(ct);
            result.AccountsUpdated++;
        }
        else
        {
            await using var ins = new SqlCommand("INSERT INTO [dbo].[AccountTypes]([Name],[Description]) VALUES(@Name,@Desc);", conn);
            ins.Parameters.AddWithValue("@Name", name.Trim());
            ins.Parameters.AddWithValue("@Desc", (object?)Null(description) ?? DBNull.Value);
            await ins.ExecuteNonQueryAsync(ct);
            result.AccountsCreated++;
        }
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

    private static AccountType Map(SqlDataReader r) => new()
    {
        TypeID      = r.GetInt32(0),
        Name        = r.GetString(1),
        Description = r.IsDBNull(2) ? string.Empty : r.GetString(2),
        IsActive    = r.GetBoolean(3),
        CreatedAt   = r.GetDateTime(4),
    };
}
