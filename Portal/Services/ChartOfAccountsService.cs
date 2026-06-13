using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class ChartOfAccountsService
{
    private readonly string                _cs;
    private readonly AccountCategoryService _catSvc;
    private readonly AccountTypeService     _typeSvc;

    public ChartOfAccountsService(string cs, AccountCategoryService catSvc, AccountTypeService typeSvc)
    {
        _cs      = cs;
        _catSvc  = catSvc;
        _typeSvc = typeSvc;
    }

    public async Task<List<ChartOfAccount>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT a.[AccountID], a.[AccountName], a.[AccountDescription],
                   a.[CategoryID], a.[TypeID], a.[IsActive], a.[CreatedAt],
                   c.[Name], t.[Name]
            FROM   [dbo].[ChartOfAccounts] a
            LEFT JOIN [dbo].[AccountCategories] c ON c.[CategoryID] = a.[CategoryID]
            LEFT JOIN [dbo].[AccountTypes]      t ON t.[TypeID]     = a.[TypeID]
            ORDER  BY a.[AccountName];
            """;
        var list = new List<ChartOfAccount>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Map(reader));
        return list;
    }

    public async Task<ChartOfAccount?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT a.[AccountID], a.[AccountName], a.[AccountDescription],
                   a.[CategoryID], a.[TypeID], a.[IsActive], a.[CreatedAt],
                   c.[Name], t.[Name]
            FROM   [dbo].[ChartOfAccounts] a
            LEFT JOIN [dbo].[AccountCategories] c ON c.[CategoryID] = a.[CategoryID]
            LEFT JOIN [dbo].[AccountTypes]      t ON t.[TypeID]     = a.[TypeID]
            WHERE  a.[AccountID] = @ID;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<int> CreateAsync(ChartOfAccount item, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [dbo].[ChartOfAccounts]([AccountName],[AccountDescription],[CategoryID],[TypeID])
            OUTPUT INSERTED.[AccountID]
            VALUES(@Name,@Desc,@CatID,@TypeID);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Name",   item.AccountName.Trim());
        cmd.Parameters.AddWithValue("@Desc",   (object?)NullIfEmpty(item.AccountDescription) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CatID",  (object?)item.CategoryID ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TypeID", (object?)item.TypeID     ?? DBNull.Value);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateAsync(ChartOfAccount item, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[ChartOfAccounts]
            SET [AccountName]=@Name,[AccountDescription]=@Desc,[CategoryID]=@CatID,[TypeID]=@TypeID
            WHERE [AccountID]=@ID;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID",     item.AccountID);
        cmd.Parameters.AddWithValue("@Name",   item.AccountName.Trim());
        cmd.Parameters.AddWithValue("@Desc",   (object?)NullIfEmpty(item.AccountDescription) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CatID",  (object?)item.CategoryID ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TypeID", (object?)item.TypeID     ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM [dbo].[ChartOfAccounts] WHERE [AccountID]=@ID;";
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
        var ws = wb.Worksheets.First();

        // Map header names → column index (1-based)
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (int c = 1; c <= lastCol; c++)
        {
            var h = ws.Cell(1, c).GetString().Trim();
            if (!string.IsNullOrEmpty(h)) headers[h] = c;
        }

        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (int row = 2; row <= lastRow; row++)
        {
            string Get(string key) => headers.TryGetValue(key, out var col) ? ws.Cell(row, col).GetString().Trim() : string.Empty;

            var accountName = Get("Account Name");
            if (string.IsNullOrWhiteSpace(accountName)) { result.RowsSkipped++; continue; }

            try
            {
                await UpsertRowAsync(accountName, Get("Account Description"), Get("Account Category"), Get("Account Type"), result, ct);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Row {row}: {ex.Message}");
                result.RowsSkipped++;
            }
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

        int lineNum = 1;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNum++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = ParseLine(line, delimiter);
            string Get(string key) => headers.TryGetValue(key, out var idx) && idx < cols.Length ? cols[idx].Trim() : string.Empty;

            var accountName = Get("Account Name");
            if (string.IsNullOrWhiteSpace(accountName)) { result.RowsSkipped++; continue; }

            try
            {
                await UpsertRowAsync(accountName, Get("Account Description"), Get("Account Category"), Get("Account Type"), result, ct);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Line {lineNum}: {ex.Message}");
                result.RowsSkipped++;
            }
        }
        return result;
    }

    private async Task UpsertRowAsync(string accountName, string accountDesc, string categoryName, string typeName, ImportResult result, CancellationToken ct)
    {
        int? categoryId = null;
        int? typeId     = null;

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            var beforeCount = await CountAsync("AccountCategories", ct);
            categoryId = await _catSvc.EnsureAsync(categoryName, ct);
            if (await CountAsync("AccountCategories", ct) > beforeCount) result.CategoriesCreated++;
        }

        if (!string.IsNullOrWhiteSpace(typeName))
        {
            var beforeCount = await CountAsync("AccountTypes", ct);
            typeId = await _typeSvc.EnsureAsync(typeName, ct);
            if (await CountAsync("AccountTypes", ct) > beforeCount) result.TypesCreated++;
        }

        const string checkSql = "SELECT [AccountID] FROM [dbo].[ChartOfAccounts] WHERE [AccountName]=@Name;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var checkCmd = new SqlCommand(checkSql, conn);
        checkCmd.Parameters.AddWithValue("@Name", accountName);
        var existing = await checkCmd.ExecuteScalarAsync(ct);

        if (existing is int existingId)
        {
            const string updSql = "UPDATE [dbo].[ChartOfAccounts] SET [AccountDescription]=@Desc,[CategoryID]=@CatID,[TypeID]=@TypeID WHERE [AccountID]=@ID;";
            await using var updCmd = new SqlCommand(updSql, conn);
            updCmd.Parameters.AddWithValue("@ID",     existingId);
            updCmd.Parameters.AddWithValue("@Desc",   (object?)NullIfEmpty(accountDesc) ?? DBNull.Value);
            updCmd.Parameters.AddWithValue("@CatID",  (object?)categoryId ?? DBNull.Value);
            updCmd.Parameters.AddWithValue("@TypeID", (object?)typeId     ?? DBNull.Value);
            await updCmd.ExecuteNonQueryAsync(ct);
            result.AccountsUpdated++;
        }
        else
        {
            const string insSql = """
                INSERT INTO [dbo].[ChartOfAccounts]([AccountName],[AccountDescription],[CategoryID],[TypeID])
                VALUES(@Name,@Desc,@CatID,@TypeID);
                """;
            await using var insCmd = new SqlCommand(insSql, conn);
            insCmd.Parameters.AddWithValue("@Name",   accountName);
            insCmd.Parameters.AddWithValue("@Desc",   (object?)NullIfEmpty(accountDesc) ?? DBNull.Value);
            insCmd.Parameters.AddWithValue("@CatID",  (object?)categoryId ?? DBNull.Value);
            insCmd.Parameters.AddWithValue("@TypeID", (object?)typeId     ?? DBNull.Value);
            await insCmd.ExecuteNonQueryAsync(ct);
            result.AccountsCreated++;
        }
    }

    private async Task<int> CountAsync(string table, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand($"SELECT COUNT(*) FROM [dbo].[{table}];", conn);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string[] ParseLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var sb     = new System.Text.StringBuilder();
        bool inQ   = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQ && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else inQ = !inQ;
            }
            else if (c == delimiter && !inQ)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return [.. fields];
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static ChartOfAccount Map(SqlDataReader r) => new()
    {
        AccountID          = r.GetInt32(0),
        AccountName        = r.GetString(1),
        AccountDescription = r.IsDBNull(2) ? string.Empty : r.GetString(2),
        CategoryID         = r.IsDBNull(3) ? null : r.GetInt32(3),
        TypeID             = r.IsDBNull(4) ? null : r.GetInt32(4),
        IsActive           = r.GetBoolean(5),
        CreatedAt          = r.GetDateTime(6),
        CategoryName       = r.IsDBNull(7) ? string.Empty : r.GetString(7),
        TypeName           = r.IsDBNull(8) ? string.Empty : r.GetString(8),
    };
}
