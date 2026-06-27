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
            ? ImportHelper.ExcelRowsAsync(ms)
            : ImportHelper.TextRowsAsync(ms, delimiter, ct);

        return await ProcessRowsAsync(rows, ct);
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
            cmd.Parameters.AddWithValue("@Desc",   (object?)ImportHelper.NullIfEmpty(item.ItemDescription) ?? DBNull.Value);
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

    private static void BindParams(SqlCommand cmd, Item i)
    {
        cmd.Parameters.AddWithValue("@Code",  i.ItemCode.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@Name",  i.ItemName.Trim());
        cmd.Parameters.AddWithValue("@Desc",  (object?)ImportHelper.NullIfEmpty(i.ItemDescription) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Price", (object?)i.TypicalPrice ?? DBNull.Value);
    }

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
