using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class EodColumnService
{
    private readonly string _cs;
    public EodColumnService(string connectionString) => _cs = connectionString;

    public async Task<List<EodColumn>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT c.[ColumnID], c.[Name], c.[Description], c.[CoaSegmentNumber],
                   ISNULL(s.[Description],'') AS [SegDesc], c.[CreatedAt], ISNULL(c.[SegmentValue],'')
            FROM   [dbo].[EodColumns]  c
            LEFT JOIN [dbo].[CoaSegments] s ON s.[SegmentNumber] = c.[CoaSegmentNumber]
            ORDER  BY c.[Name];
            """;
        var list = new List<EodColumn>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Map(reader));
        return list;
    }

    public async Task<int> CreateAsync(EodColumn item, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [dbo].[EodColumns]([Name],[Description],[CoaSegmentNumber],[SegmentValue])
            OUTPUT INSERTED.[ColumnID]
            VALUES(@Name,@Desc,@Seg,@SegVal);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Name",   item.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc",   (object?)ImportHelper.NullIfEmpty(item.Description) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Seg",    item.CoaSegmentNumber);
        cmd.Parameters.AddWithValue("@SegVal", (object?)ImportHelper.NullIfEmpty(item.SegmentValue) ?? DBNull.Value);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateAsync(EodColumn item, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[EodColumns]
            SET [Name]=@Name,[Description]=@Desc,[CoaSegmentNumber]=@Seg,[SegmentValue]=@SegVal
            WHERE [ColumnID]=@ID;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID",     item.ColumnID);
        cmd.Parameters.AddWithValue("@Name",   item.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc",   (object?)ImportHelper.NullIfEmpty(item.Description) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Seg",    item.CoaSegmentNumber);
        cmd.Parameters.AddWithValue("@SegVal", (object?)ImportHelper.NullIfEmpty(item.SegmentValue) ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM [dbo].[EodColumns] WHERE [ColumnID]=@ID;";
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
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (int row = 2; row <= lastRow; row++)
        {
            string Get(string key) => headers.TryGetValue(key, out var col) ? ws.Cell(row, col).GetString().Trim() : string.Empty;
            int seg = int.TryParse(Get("CoA Segment"), out var s) ? s : 0;
            try   { await UpsertRowAsync(Get("Name"), Get("Description"), seg, result, ct, Get("Segment Value")); }
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
            int seg = int.TryParse(Get("CoA Segment"), out var s) ? s : 0;
            try   { await UpsertRowAsync(Get("Name"), Get("Description"), seg, result, ct, Get("Segment Value")); }
            catch (Exception ex) { result.Errors.Add($"Line {lineNum}: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task UpsertRowAsync(string name, string description, int coaSegmentNumber, ImportResult result, CancellationToken ct, string segmentValue = "")
    {
        if (string.IsNullOrWhiteSpace(name)) { result.RowsSkipped++; return; }
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var checkCmd = new SqlCommand("SELECT [ColumnID] FROM [dbo].[EodColumns] WHERE [Name]=@Name;", conn);
        checkCmd.Parameters.AddWithValue("@Name", name.Trim());
        var existing = await checkCmd.ExecuteScalarAsync(ct);
        if (existing is int id)
        {
            await using var upd = new SqlCommand(
                "UPDATE [dbo].[EodColumns] SET [Description]=@Desc,[CoaSegmentNumber]=@Seg,[SegmentValue]=@SV WHERE [ColumnID]=@ID;", conn);
            upd.Parameters.AddWithValue("@ID",   id);
            upd.Parameters.AddWithValue("@Desc", (object?)ImportHelper.NullIfEmpty(description) ?? DBNull.Value);
            upd.Parameters.AddWithValue("@Seg",  coaSegmentNumber);
            upd.Parameters.AddWithValue("@SV",   (object?)ImportHelper.NullIfEmpty(segmentValue) ?? DBNull.Value);
            await upd.ExecuteNonQueryAsync(ct);
            result.AccountsUpdated++;
        }
        else
        {
            await using var ins = new SqlCommand(
                "INSERT INTO [dbo].[EodColumns]([Name],[Description],[CoaSegmentNumber],[SegmentValue]) VALUES(@Name,@Desc,@Seg,@SV);", conn);
            ins.Parameters.AddWithValue("@Name", name.Trim());
            ins.Parameters.AddWithValue("@Desc", (object?)ImportHelper.NullIfEmpty(description) ?? DBNull.Value);
            ins.Parameters.AddWithValue("@Seg",  coaSegmentNumber);
            ins.Parameters.AddWithValue("@SV",   (object?)ImportHelper.NullIfEmpty(segmentValue) ?? DBNull.Value);
            await ins.ExecuteNonQueryAsync(ct);
            result.AccountsCreated++;
        }
    }

    private static EodColumn Map(SqlDataReader r) => new()
    {
        ColumnID              = r.GetInt32(0),
        Name                  = r.GetString(1),
        Description           = r.IsDBNull(2) ? string.Empty : r.GetString(2),
        CoaSegmentNumber      = r.GetInt32(3),
        CoaSegmentDescription = r.GetString(4),
        CreatedAt             = r.GetDateTime(5),
        SegmentValue          = r.GetString(6),
    };
}
