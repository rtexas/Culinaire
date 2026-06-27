using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class EodRowService
{
    private readonly string _cs;
    public EodRowService(string connectionString) => _cs = connectionString;

    public async Task<List<EodRow>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT r.[RowID], r.[Name], r.[Description], r.[SectionID],
                   ISNULL(s.[Name],'') AS [SectionName], r.[CreatedAt]
            FROM   [dbo].[EodRows]     r
            LEFT JOIN [dbo].[EodSections] s ON s.[SectionID] = r.[SectionID]
            ORDER  BY s.[Name], r.[Name];
            """;
        var list = new List<EodRow>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Map(reader));
        return list;
    }

    public async Task<int> CreateAsync(EodRow item, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [dbo].[EodRows]([Name],[Description],[SectionID])
            OUTPUT INSERTED.[RowID]
            VALUES(@Name,@Desc,@SectionID);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Name",      item.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc",      (object?)ImportHelper.NullIfEmpty(item.Description) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SectionID", item.SectionID);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateAsync(EodRow item, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[EodRows]
            SET [Name]=@Name,[Description]=@Desc,[SectionID]=@SectionID
            WHERE [RowID]=@ID;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID",        item.RowID);
        cmd.Parameters.AddWithValue("@Name",      item.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc",      (object?)ImportHelper.NullIfEmpty(item.Description) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SectionID", item.SectionID);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM [dbo].[EodRows] WHERE [RowID]=@ID;";
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

    // Columns that MUST be present for this file to be accepted as an EOD Rows import.
    private static readonly string[] RequiredColumns = ["Name", "Section"];

    private static ImportResult? ValidateHeaders(IReadOnlySet<string> found)
    {
        var missing = RequiredColumns
            .Where(c => !found.Contains(c))
            .ToList();
        if (missing.Count == 0) return null;
        var result = new ImportResult();
        result.Errors.Add(
            $"Wrong file — this does not look like an EOD Rows file. " +
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
            try   { await UpsertRowAsync(Get("Name"), Get("Description"), Get("Section"), result, ct); }
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
        if (ValidateHeaders(headers.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)) is { } bad) return bad;
        int lineNum = 1;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNum++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ImportHelper.ParseLine(line, delimiter);
            string Get(string key) => headers.TryGetValue(key, out var idx) && idx < cols.Length ? cols[idx].Trim() : string.Empty;
            try   { await UpsertRowAsync(Get("Name"), Get("Description"), Get("Section"), result, ct); }
            catch (Exception ex) { result.Errors.Add($"Line {lineNum}: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task UpsertRowAsync(string name, string description, string sectionName, ImportResult result, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) { result.RowsSkipped++; return; }

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        // Resolve section by name (0 = unassigned if blank or not found)
        int sectionId = 0;
        if (!string.IsNullOrWhiteSpace(sectionName))
        {
            await using var secCmd = new SqlCommand(
                "SELECT [SectionID] FROM [dbo].[EodSections] WHERE [Name]=@SName;", conn);
            secCmd.Parameters.AddWithValue("@SName", sectionName.Trim());
            var secResult = await secCmd.ExecuteScalarAsync(ct);
            if (secResult is int sid) sectionId = sid;
            else { result.Errors.Add($"Section \"{sectionName}\" not found — row \"{name}\" skipped."); result.RowsSkipped++; return; }
        }

        await using var checkCmd = new SqlCommand("SELECT [RowID] FROM [dbo].[EodRows] WHERE [Name]=@Name;", conn);
        checkCmd.Parameters.AddWithValue("@Name", name.Trim());
        var existing = await checkCmd.ExecuteScalarAsync(ct);
        if (existing is int id)
        {
            await using var upd = new SqlCommand(
                "UPDATE [dbo].[EodRows] SET [Description]=@Desc,[SectionID]=@Sec WHERE [RowID]=@ID;", conn);
            upd.Parameters.AddWithValue("@ID",   id);
            upd.Parameters.AddWithValue("@Desc", (object?)ImportHelper.NullIfEmpty(description) ?? DBNull.Value);
            upd.Parameters.AddWithValue("@Sec",  sectionId);
            await upd.ExecuteNonQueryAsync(ct);
            result.AccountsUpdated++;
        }
        else
        {
            await using var ins = new SqlCommand(
                "INSERT INTO [dbo].[EodRows]([Name],[Description],[SectionID]) VALUES(@Name,@Desc,@Sec);", conn);
            ins.Parameters.AddWithValue("@Name", name.Trim());
            ins.Parameters.AddWithValue("@Desc", (object?)ImportHelper.NullIfEmpty(description) ?? DBNull.Value);
            ins.Parameters.AddWithValue("@Sec",  sectionId);
            await ins.ExecuteNonQueryAsync(ct);
            result.AccountsCreated++;
        }
    }

    private static EodRow Map(SqlDataReader r) => new()
    {
        RowID       = r.GetInt32(0),
        Name        = r.GetString(1),
        Description = r.IsDBNull(2) ? string.Empty : r.GetString(2),
        SectionID   = r.GetInt32(3),
        SectionName = r.GetString(4),
        CreatedAt   = r.GetDateTime(5),
    };
}
