using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class EodSectionService
{
    private readonly string _cs;
    public EodSectionService(string connectionString) => _cs = connectionString;

    public async Task<List<EodSection>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT [SectionID],[Name],[Description],[Multiplier],[UseInEodSales],[UseInEodGraph],[CreatedAt] FROM [dbo].[EodSections] ORDER BY [Name];";
        var list = new List<EodSection>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Map(reader));
        return list;
    }

    public async Task<int> CreateAsync(EodSection item, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [dbo].[EodSections]([Name],[Description],[Multiplier],[UseInEodSales],[UseInEodGraph])
            OUTPUT INSERTED.[SectionID]
            VALUES(@Name,@Desc,@Mult,@UseInEodSales,@UseInEodGraph);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Name",         item.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc",         (object?)NullIfEmpty(item.Description) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Mult",         item.Multiplier);
        cmd.Parameters.AddWithValue("@UseInEodSales",item.UseInEodSales ? 1 : 0);
        cmd.Parameters.AddWithValue("@UseInEodGraph", item.UseInEodGraph ? 1 : 0);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateAsync(EodSection item, CancellationToken ct = default)
    {
        const string sql = "UPDATE [dbo].[EodSections] SET [Name]=@Name,[Description]=@Desc,[Multiplier]=@Mult,[UseInEodSales]=@UseInEodSales,[UseInEodGraph]=@UseInEodGraph WHERE [SectionID]=@ID;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID",           item.SectionID);
        cmd.Parameters.AddWithValue("@Name",         item.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc",         (object?)NullIfEmpty(item.Description) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Mult",         item.Multiplier);
        cmd.Parameters.AddWithValue("@UseInEodSales",item.UseInEodSales ? 1 : 0);
        cmd.Parameters.AddWithValue("@UseInEodGraph", item.UseInEodGraph ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM [dbo].[EodSections] WHERE [SectionID]=@ID;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

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

    // Columns that MUST be present for this file to be accepted as an EOD Sections import.
    private static readonly string[] RequiredColumns = ["Name", "Multiplier"];

    private static ImportResult? ValidateHeaders(IReadOnlySet<string> found)
    {
        var missing = RequiredColumns
            .Where(c => !found.Contains(c))
            .ToList();
        if (missing.Count == 0) return null;
        var result = new ImportResult();
        result.Errors.Add(
            $"Wrong file — this does not look like an EOD Sections file. " +
            $"Missing required column(s): {string.Join(", ", missing)}. " +
            $"Expected columns include: {string.Join(", ", RequiredColumns)}.");
        return result;
    }

    private async Task<ImportResult> ImportExcelAsync(Stream stream, CancellationToken ct)
    {
        var result = new ImportResult();
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (int c = 1; c <= lastCol; c++)
        {
            var h = ws.Cell(1, c).GetString().Trim();
            if (!string.IsNullOrEmpty(h)) headers[h] = c;
        }
        if (ValidateHeaders(headers.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)) is { } bad) return bad;
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (int row = 2; row <= lastRow; row++)
        {
            string Get(string key) => headers.TryGetValue(key, out var col) ? ws.Cell(row, col).GetString().Trim() : string.Empty;
            int  mult          = int.TryParse(Get("Multiplier"), out var m) && m is -1 or 0 or 1 ? m : 1;
            bool useInEodSales = ParseBoolFlag(Get("Used in EOD Sales"), defaultValue: true);
            bool useInEodGraph = ParseBoolFlag(Get("Used in EOD Graph"), defaultValue: false);
            try   { await UpsertRowAsync(Get("Name"), Get("Description"), mult, useInEodSales, useInEodGraph, result, ct); }
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
        var headers = ParseLine(headerLine, delimiter)
            .Select((h, i) => (h.Trim(), i))
            .Where(x => !string.IsNullOrEmpty(x.Item1))
            .ToDictionary(x => x.Item1, x => x.i, StringComparer.OrdinalIgnoreCase);
        if (ValidateHeaders(headers.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)) is { } bad) return bad;
        int lineNum = 1;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNum++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ParseLine(line, delimiter);
            string Get(string key) => headers.TryGetValue(key, out var idx) && idx < cols.Length ? cols[idx].Trim() : string.Empty;
            int  mult          = int.TryParse(Get("Multiplier"), out var m) && m is -1 or 0 or 1 ? m : 1;
            bool useInEodSales = ParseBoolFlag(Get("Used in EOD Sales"), defaultValue: true);
            bool useInEodGraph = ParseBoolFlag(Get("Used in EOD Graph"), defaultValue: false);
            try   { await UpsertRowAsync(Get("Name"), Get("Description"), mult, useInEodSales, useInEodGraph, result, ct); }
            catch (Exception ex) { result.Errors.Add($"Line {lineNum}: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task UpsertRowAsync(string name, string description, int multiplier, bool useInEodSales, bool useInEodGraph, ImportResult result, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) { result.RowsSkipped++; return; }
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var checkCmd = new SqlCommand("SELECT [SectionID] FROM [dbo].[EodSections] WHERE [Name]=@Name;", conn);
        checkCmd.Parameters.AddWithValue("@Name", name.Trim());
        var existing = await checkCmd.ExecuteScalarAsync(ct);
        if (existing is int id)
        {
            await using var upd = new SqlCommand(
                "UPDATE [dbo].[EodSections] SET [Description]=@Desc,[Multiplier]=@Mult,[UseInEodSales]=@UseInEodSales,[UseInEodGraph]=@UseInEodGraph WHERE [SectionID]=@ID;", conn);
            upd.Parameters.AddWithValue("@ID",            id);
            upd.Parameters.AddWithValue("@Desc",          (object?)NullIfEmpty(description) ?? DBNull.Value);
            upd.Parameters.AddWithValue("@Mult",          multiplier);
            upd.Parameters.AddWithValue("@UseInEodSales", useInEodSales ? 1 : 0);
            upd.Parameters.AddWithValue("@UseInEodGraph", useInEodGraph ? 1 : 0);
            await upd.ExecuteNonQueryAsync(ct);
            result.AccountsUpdated++;
        }
        else
        {
            await using var ins = new SqlCommand(
                "INSERT INTO [dbo].[EodSections]([Name],[Description],[Multiplier],[UseInEodSales],[UseInEodGraph]) VALUES(@Name,@Desc,@Mult,@UseInEodSales,@UseInEodGraph);", conn);
            ins.Parameters.AddWithValue("@Name",          name.Trim());
            ins.Parameters.AddWithValue("@Desc",          (object?)NullIfEmpty(description) ?? DBNull.Value);
            ins.Parameters.AddWithValue("@Mult",          multiplier);
            ins.Parameters.AddWithValue("@UseInEodSales", useInEodSales ? 1 : 0);
            ins.Parameters.AddWithValue("@UseInEodGraph", useInEodGraph ? 1 : 0);
            await ins.ExecuteNonQueryAsync(ct);
            result.AccountsCreated++;
        }
    }

    /// <summary>Accepts 0/false/no → false; 1/true/yes → true; empty → defaultValue.</summary>
    private static bool ParseBoolFlag(string value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        if (value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("no",    StringComparison.OrdinalIgnoreCase)) return false;
        if (value == "1" || value.Equals("true",  StringComparison.OrdinalIgnoreCase) ||
            value.Equals("yes",   StringComparison.OrdinalIgnoreCase)) return true;
        return defaultValue;
    }

    private static string[] ParseLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var sb     = new System.Text.StringBuilder();
        bool inQ   = false;
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

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static EodSection Map(SqlDataReader r) => new()
    {
        SectionID    = r.GetInt32(0),
        Name         = r.GetString(1),
        Description  = r.IsDBNull(2) ? string.Empty : r.GetString(2),
        Multiplier   = r.GetInt32(3),
        UseInEodSales= r.GetBoolean(4),
        UseInEodGraph= r.GetBoolean(5),
        CreatedAt    = r.GetDateTime(6),
    };
}
