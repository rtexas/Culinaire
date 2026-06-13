using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class ItemService
{
    private readonly string _cs;
    public ItemService(string connectionString) => _cs = connectionString;

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<List<Item>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT [ItemID],[ItemCode],[ItemName],[ItemDescription],[TypicalPrice],[IsActive],[CreatedAt]
            FROM   [dbo].[Items]
            ORDER  BY [ItemCode];
            """;
        var list = new List<Item>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) list.Add(Map(reader));
        return list;
    }

    public async Task<int> CreateAsync(Item item, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [dbo].[Items]([ItemCode],[ItemName],[ItemDescription],[TypicalPrice])
            OUTPUT INSERTED.[ItemID]
            VALUES(@Code,@Name,@Desc,@Price);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        BindParams(cmd, item);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateAsync(Item item, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[Items]
            SET [ItemCode]=@Code,[ItemName]=@Name,[ItemDescription]=@Desc,[TypicalPrice]=@Price
            WHERE [ItemID]=@ItemID;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ItemID", item.ItemID);
        BindParams(cmd, item);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("DELETE FROM [dbo].[Items] WHERE [ItemID]=@ID;", conn);
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
        IAsyncEnumerable<Dictionary<string, string>> rows = ext is ".xlsx" or ".xls"
            ? ExcelRows(ms)
            : TextRows(ms, delimiter, ct);

        return await ProcessRowsAsync(rows, ct);
    }

    private static async IAsyncEnumerable<Dictionary<string, string>> ExcelRows(Stream ms)
    {
        using var wb = new XLWorkbook(ms);
        var ws       = wb.Worksheets.First();
        var headers  = BuildHeaderMap(ws);
        int lastRow  = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (int row = 2; row <= lastRow; row++)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, col) in headers)
                d[key] = ws.Cell(row, col).GetString().Trim();
            yield return d;
            await Task.CompletedTask;
        }
    }

    private static async IAsyncEnumerable<Dictionary<string, string>> TextRows(
        Stream ms, char delimiter,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reader = new StreamReader(ms, detectEncodingFromByteOrderMarks: true);
        var headerLine   = await reader.ReadLineAsync(ct);
        if (headerLine is null) yield break;

        var headers = ParseLine(headerLine, delimiter)
            .Select((h, i) => (h.Trim(), i))
            .Where(x => !string.IsNullOrEmpty(x.Item1))
            .ToDictionary(x => x.Item1, x => x.i, StringComparer.OrdinalIgnoreCase);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ParseLine(line, delimiter);
            var d    = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, idx) in headers)
                d[key] = idx < cols.Length ? cols[idx].Trim() : string.Empty;
            yield return d;
        }
    }

    private async Task<ImportResult> ProcessRowsAsync(IAsyncEnumerable<Dictionary<string, string>> rows, CancellationToken ct)
    {
        var result = new ImportResult();
        int rowNum = 1;

        await foreach (var row in rows.WithCancellation(ct))
        {
            rowNum++;
            string G(string k) => row.TryGetValue(k, out var v) ? v.Trim() : string.Empty;

            var code = G("ItemCode").ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code)) { result.RowsSkipped++; continue; }

            var name = G("ItemName");
            if (string.IsNullOrWhiteSpace(name)) { result.RowsSkipped++; continue; }

            decimal? price = null;
            var priceStr = G("TypicalPrice").TrimStart('$');
            if (!string.IsNullOrWhiteSpace(priceStr) && decimal.TryParse(priceStr, out var p))
                price = p;

            try
            {
                var item = new Item
                {
                    ItemCode        = code,
                    ItemName        = name,
                    ItemDescription = G("ItemDescription"),
                    TypicalPrice    = price,
                };
                await UpsertAsync(item, result, ct);
            }
            catch (Exception ex) { result.Errors.Add($"Row {rowNum}: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task UpsertAsync(Item item, ImportResult result, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        await using var chk = new SqlCommand("SELECT [ItemID] FROM [dbo].[Items] WHERE [ItemCode]=@Code;", conn);
        chk.Parameters.AddWithValue("@Code", item.ItemCode);
        var existing = await chk.ExecuteScalarAsync(ct);

        if (existing is int id)
        {
            const string upd = """
                UPDATE [dbo].[Items]
                SET [ItemName]=@Name,[ItemDescription]=@Desc,[TypicalPrice]=@Price
                WHERE [ItemID]=@ItemID;
                """;
            await using var cmd = new SqlCommand(upd, conn);
            cmd.Parameters.AddWithValue("@ItemID", id);
            cmd.Parameters.AddWithValue("@Name",   item.ItemName.Trim());
            cmd.Parameters.AddWithValue("@Desc",   (object?)Null(item.ItemDescription) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Price",  (object?)item.TypicalPrice ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            result.AccountsUpdated++;
        }
        else
        {
            const string ins = """
                INSERT INTO [dbo].[Items]([ItemCode],[ItemName],[ItemDescription],[TypicalPrice])
                VALUES(@Code,@Name,@Desc,@Price);
                """;
            await using var cmd = new SqlCommand(ins, conn);
            BindParams(cmd, item);
            await cmd.ExecuteNonQueryAsync(ct);
            result.AccountsCreated++;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void BindParams(SqlCommand cmd, Item i)
    {
        cmd.Parameters.AddWithValue("@Code",  i.ItemCode.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@Name",  i.ItemName.Trim());
        cmd.Parameters.AddWithValue("@Desc",  (object?)Null(i.ItemDescription) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Price", (object?)i.TypicalPrice ?? DBNull.Value);
    }

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

    private static Item Map(SqlDataReader r) => new()
    {
        ItemID          = r.GetInt32(0),
        ItemCode        = r.GetString(1),
        ItemName        = r.GetString(2),
        ItemDescription = r.IsDBNull(3) ? string.Empty : r.GetString(3),
        TypicalPrice    = r.IsDBNull(4) ? null : r.GetDecimal(4),
        IsActive        = r.GetBoolean(5),
        CreatedAt       = r.GetDateTime(6),
    };
}
