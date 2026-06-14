using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class ChartOfAccountsService
{
    private readonly string _cs;
    private readonly AccountCategoryService _categories;
    private readonly AccountTypeService     _types;

    public ChartOfAccountsService(string connectionString, AccountCategoryService categories, AccountTypeService types)
    {
        _cs         = connectionString;
        _categories = categories;
        _types      = types;
    }

    // ── Segment delimiter setting ─────────────────────────────────────────────

    public async Task<string> GetDelimiterAsync(CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "SELECT TOP 1 [Value] FROM [dbo].[Settings] WHERE [Name]='CoaAccountDelimiter';", conn);
        var v = await cmd.ExecuteScalarAsync(ct);
        return v is string s && s.Length > 0 ? s : "-";
    }

    public async Task SaveDelimiterAsync(string delimiter, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("""
            IF EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name]='CoaAccountDelimiter')
                UPDATE [dbo].[Settings] SET [Value]=@V WHERE [Name]='CoaAccountDelimiter';
            ELSE
                INSERT INTO [dbo].[Settings]([Name],[Value],[IsEnabled]) VALUES('CoaAccountDelimiter',@V,1);
            """, conn);
        cmd.Parameters.AddWithValue("@V", delimiter ?? "-");
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Read all (admin list) ─────────────────────────────────────────────────

    public async Task<List<ChartOfAccount>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT a.[AccountID],a.[AccountName],ISNULL(a.[AccountDescription],''),
                   a.[CategoryID],a.[TypeID],a.[IsActive],
                   ISNULL(c.[Name],''),ISNULL(t.[Name],''),
                   ISNULL(a.[FullAccountString],''),
                   ISNULL(a.[Seg1Value],''),ISNULL(a.[Seg2Value],''),ISNULL(a.[Seg3Value],''),
                   ISNULL(a.[Seg4Value],''),ISNULL(a.[Seg5Value],''),ISNULL(a.[Seg6Value],''),
                   a.[CreatedAt]
            FROM   [dbo].[ChartOfAccounts] a
            LEFT JOIN [dbo].[AccountCategories] c ON c.[CategoryID]=a.[CategoryID]
            LEFT JOIN [dbo].[AccountTypes]      t ON t.[TypeID]     =a.[TypeID]
            ORDER  BY a.[AccountName];
            """;
        var list = new List<ChartOfAccount>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) list.Add(Map(reader));
        return list;
    }

    /// <summary>Returns active accounts in the Expense category filtered by location segment for use in Check Setup.</summary>
    public async Task<List<ChartOfAccount>> GetExpenseAccountsForLocationAsync(
        int locationSegNum, string locationSegVal, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT a.[AccountID],a.[AccountName],ISNULL(a.[AccountDescription],''),
                   a.[CategoryID],a.[TypeID],a.[IsActive],
                   ISNULL(c.[Name],''),ISNULL(t.[Name],''),
                   ISNULL(a.[FullAccountString],''),
                   ISNULL(a.[Seg1Value],''),ISNULL(a.[Seg2Value],''),ISNULL(a.[Seg3Value],''),
                   ISNULL(a.[Seg4Value],''),ISNULL(a.[Seg5Value],''),ISNULL(a.[Seg6Value],''),
                   a.[CreatedAt]
            FROM   [dbo].[ChartOfAccounts] a
            LEFT JOIN [dbo].[AccountCategories] c ON c.[CategoryID]=a.[CategoryID]
            LEFT JOIN [dbo].[AccountTypes]      t ON t.[TypeID]     =a.[TypeID]
            WHERE  a.[IsActive]=1
              AND  c.[Name]='Expense'
              AND  (@LocSeg=0 OR {SegCase("@LocSeg")} = @LocVal
                             OR ISNULL(a.[Seg1Value],'') = @LocVal)
            ORDER  BY ISNULL(a.[FullAccountString],a.[AccountName]);
            """;
        var list = new List<ChartOfAccount>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@LocSeg", locationSegNum);
        cmd.Parameters.AddWithValue("@LocVal", locationSegVal);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) list.Add(Map(reader));
        return list;
    }

    /// <summary>Returns active accounts filtered by location segment; caller further filters by column using GetSegValue().</summary>
    public async Task<List<GlAccount>> GetForLocationAsync(
        int locationSegNum, string locationSegVal, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT [AccountID],ISNULL([FullAccountString],[AccountName]),
                   ISNULL([AccountDescription],''),[IsActive],
                   ISNULL([Seg1Value],''),ISNULL([Seg2Value],''),ISNULL([Seg3Value],''),
                   ISNULL([Seg4Value],''),ISNULL([Seg5Value],''),ISNULL([Seg6Value],''),[CreatedAt]
            FROM   [dbo].[ChartOfAccounts]
            WHERE  [IsActive]=1
              AND  (@LocSeg=0 OR {SegCase("@LocSeg")} = @LocVal)
            ORDER  BY ISNULL([FullAccountString],[AccountName]);
            """;
        var list = new List<GlAccount>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@LocSeg", locationSegNum);
        cmd.Parameters.AddWithValue("@LocVal", locationSegVal);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new GlAccount
            {
                AccountID         = r.GetInt32(0),
                FullAccountString = r.GetString(1),
                Description       = r.GetString(2),
                IsActive          = r.GetBoolean(3),
                Seg1Value         = r.GetString(4), Seg2Value = r.GetString(5), Seg3Value = r.GetString(6),
                Seg4Value         = r.GetString(7), Seg5Value = r.GetString(8), Seg6Value = r.GetString(9),
                CreatedAt         = r.GetDateTime(10),
            });
        return list;
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<int> CreateAsync(ChartOfAccount a, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [dbo].[ChartOfAccounts]
                ([AccountName],[AccountDescription],[CategoryID],[TypeID],[IsActive],
                 [FullAccountString],[Seg1Value],[Seg2Value],[Seg3Value],[Seg4Value],[Seg5Value],[Seg6Value])
            OUTPUT INSERTED.[AccountID]
            VALUES(@Name,@Desc,@CatID,@TypeID,@Active,@Full,@S1,@S2,@S3,@S4,@S5,@S6);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        AddParams(cmd, a);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateAsync(ChartOfAccount a, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[ChartOfAccounts]
            SET [AccountName]=@Name,[AccountDescription]=@Desc,[CategoryID]=@CatID,[TypeID]=@TypeID,
                [IsActive]=@Active,[FullAccountString]=@Full,
                [Seg1Value]=@S1,[Seg2Value]=@S2,[Seg3Value]=@S3,[Seg4Value]=@S4,[Seg5Value]=@S5,[Seg6Value]=@S6
            WHERE [AccountID]=@ID;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", a.AccountID);
        AddParams(cmd, a);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(int accountID, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM [dbo].[ChartOfAccounts] WHERE [AccountID]=@ID;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", accountID);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Import ────────────────────────────────────────────────────────────────

    public async Task<ImportResult> ImportAsync(Stream stream, string fileName, char delimiter, string segDelimiter = "-", CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".xlsx" or ".xls"
            ? await ImportExcelAsync(ms, segDelimiter, ct)
            : await ImportTextAsync(ms, delimiter, segDelimiter, ct);
    }

    private async Task<ImportResult> ImportExcelAsync(Stream stream, string segDelimiter, CancellationToken ct)
    {
        var result = new ImportResult();
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var headers = ReadHeaders(ws);
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (int row = 2; row <= lastRow; row++)
        {
            string Get(string key) => headers.TryGetValue(key, out var c) ? ws.Cell(row, c).GetString().Trim() : string.Empty;
            try   { await UpsertAsync(Get("Account Name"), Get("Account Description"), Get("Account Category"), Get("Account Type"), Get("Account"), IsActiveVal(Get("Active")), segDelimiter, result, ct); }
            catch (Exception ex) { result.Errors.Add($"Row {row}: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task<ImportResult> ImportTextAsync(Stream stream, char delimiter, string segDelimiter, CancellationToken ct)
    {
        var result = new ImportResult();
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine is null) return result;
        var headers = ParseLine(headerLine, delimiter)
            .Select((h, i) => (h.Trim(), i))
            .Where(x => !string.IsNullOrEmpty(x.Item1))
            .ToDictionary(x => x.Item1, x => x.i, StringComparer.OrdinalIgnoreCase);
        int lineNum = 1; string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNum++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ParseLine(line, delimiter);
            string Get(string key) => headers.TryGetValue(key, out var idx) && idx < cols.Length ? cols[idx].Trim() : string.Empty;
            try   { await UpsertAsync(Get("Account Name"), Get("Account Description"), Get("Account Category"), Get("Account Type"), Get("Account"), IsActiveVal(Get("Active")), segDelimiter, result, ct); }
            catch (Exception ex) { result.Errors.Add($"Line {lineNum}: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task UpsertAsync(string accountName, string description, string categoryName, string typeName,
        string fullAccountString, bool isActive, string segDelimiter, ImportResult result, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accountName) && string.IsNullOrWhiteSpace(fullAccountString))
        { result.RowsSkipped++; return; }

        // Resolve display name
        var displayName = NullIfEmpty(accountName) ?? fullAccountString.Trim();

        // Resolve category — EnsureAsync creates if missing; we approximate "new" by checking the list
        int? categoryID = null;
        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            var allCats = await _categories.GetAllAsync(ct);
            bool existed = allCats.Any(c => c.Name.Equals(categoryName.Trim(), StringComparison.OrdinalIgnoreCase));
            categoryID = await _categories.EnsureAsync(categoryName.Trim(), ct);
            if (!existed) result.CategoriesCreated++;
        }

        // Resolve type
        int? typeID = null;
        if (!string.IsNullOrWhiteSpace(typeName))
        {
            var allTypes = await _types.GetAllAsync(ct);
            bool existed = allTypes.Any(t => t.Name.Equals(typeName.Trim(), StringComparison.OrdinalIgnoreCase));
            typeID = await _types.EnsureAsync(typeName.Trim(), ct);
            if (!existed) result.TypesCreated++;
        }

        // Parse segment values
        var parts = string.IsNullOrWhiteSpace(fullAccountString)
            ? Array.Empty<string>()
            : fullAccountString.Split(segDelimiter, StringSplitOptions.None);
        string Seg(int i) => i < parts.Length ? parts[i].Trim() : string.Empty;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var check = new SqlCommand(
            "SELECT [AccountID] FROM [dbo].[ChartOfAccounts] WHERE [AccountName]=@N;", conn);
        check.Parameters.AddWithValue("@N", displayName);
        var existing = await check.ExecuteScalarAsync(ct);

        if (existing is int id)
        {
            await using var upd = new SqlCommand("""
                UPDATE [dbo].[ChartOfAccounts]
                SET [AccountDescription]=@Desc,[CategoryID]=@Cat,[TypeID]=@Type,[IsActive]=@Active,
                    [FullAccountString]=@Full,[Seg1Value]=@S1,[Seg2Value]=@S2,[Seg3Value]=@S3,
                    [Seg4Value]=@S4,[Seg5Value]=@S5,[Seg6Value]=@S6
                WHERE [AccountID]=@ID;
                """, conn);
            upd.Parameters.AddWithValue("@ID",    id);
            upd.Parameters.AddWithValue("@Desc",  (object?)NullIfEmpty(description)        ?? DBNull.Value);
            upd.Parameters.AddWithValue("@Cat",   (object?)categoryID                       ?? DBNull.Value);
            upd.Parameters.AddWithValue("@Type",  (object?)typeID                            ?? DBNull.Value);
            upd.Parameters.AddWithValue("@Active", isActive);
            upd.Parameters.AddWithValue("@Full",  (object?)NullIfEmpty(fullAccountString)   ?? DBNull.Value);
            AddSegParams(upd, Seg(0), Seg(1), Seg(2), Seg(3), Seg(4), Seg(5));
            await upd.ExecuteNonQueryAsync(ct);
            result.AccountsUpdated++;
        }
        else
        {
            await using var ins = new SqlCommand("""
                INSERT INTO [dbo].[ChartOfAccounts]
                    ([AccountName],[AccountDescription],[CategoryID],[TypeID],[IsActive],
                     [FullAccountString],[Seg1Value],[Seg2Value],[Seg3Value],[Seg4Value],[Seg5Value],[Seg6Value])
                VALUES(@Name,@Desc,@Cat,@Type,@Active,@Full,@S1,@S2,@S3,@S4,@S5,@S6);
                """, conn);
            ins.Parameters.AddWithValue("@Name",  displayName);
            ins.Parameters.AddWithValue("@Desc",  (object?)NullIfEmpty(description)        ?? DBNull.Value);
            ins.Parameters.AddWithValue("@Cat",   (object?)categoryID                       ?? DBNull.Value);
            ins.Parameters.AddWithValue("@Type",  (object?)typeID                            ?? DBNull.Value);
            ins.Parameters.AddWithValue("@Active", isActive);
            ins.Parameters.AddWithValue("@Full",  (object?)NullIfEmpty(fullAccountString)   ?? DBNull.Value);
            AddSegParams(ins, Seg(0), Seg(1), Seg(2), Seg(3), Seg(4), Seg(5));
            await ins.ExecuteNonQueryAsync(ct);
            result.AccountsCreated++;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string SegCase(string param) => $"""
        CASE {param}
            WHEN 1 THEN ISNULL([Seg1Value],'') WHEN 2 THEN ISNULL([Seg2Value],'') WHEN 3 THEN ISNULL([Seg3Value],'')
            WHEN 4 THEN ISNULL([Seg4Value],'') WHEN 5 THEN ISNULL([Seg5Value],'') WHEN 6 THEN ISNULL([Seg6Value],'')
            ELSE ''
        END
        """;

    private static void AddParams(SqlCommand cmd, ChartOfAccount a)
    {
        cmd.Parameters.AddWithValue("@Name",   a.AccountName.Trim());
        cmd.Parameters.AddWithValue("@Desc",   (object?)NullIfEmpty(a.AccountDescription) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CatID",  (object?)a.CategoryID                      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TypeID", (object?)a.TypeID                           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Active",  a.IsActive);
        cmd.Parameters.AddWithValue("@Full",   (object?)NullIfEmpty(a.FullAccountString)   ?? DBNull.Value);
        AddSegParams(cmd, a.Seg1Value, a.Seg2Value, a.Seg3Value, a.Seg4Value, a.Seg5Value, a.Seg6Value);
    }

    private static void AddSegParams(SqlCommand cmd, string s1, string s2, string s3, string s4, string s5, string s6)
    {
        cmd.Parameters.AddWithValue("@S1", (object?)NullIfEmpty(s1) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@S2", (object?)NullIfEmpty(s2) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@S3", (object?)NullIfEmpty(s3) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@S4", (object?)NullIfEmpty(s4) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@S5", (object?)NullIfEmpty(s5) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@S6", (object?)NullIfEmpty(s6) ?? DBNull.Value);
    }

    private static Dictionary<string, int> ReadHeaders(IXLWorksheet ws)
    {
        var h = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int last = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (int c = 1; c <= last; c++) { var v = ws.Cell(1, c).GetString().Trim(); if (!string.IsNullOrEmpty(v)) h[v] = c; }
        return h;
    }

    private static string[] ParseLine(string line, char delimiter)
    {
        var fields = new List<string>(); var sb = new System.Text.StringBuilder(); bool inQ = false;
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

    private static bool IsActiveVal(string v) =>
        v == "" || v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase);

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static ChartOfAccount Map(SqlDataReader r) => new()
    {
        AccountID          = r.GetInt32(0),
        AccountName        = r.GetString(1),
        AccountDescription = r.GetString(2),
        CategoryID         = r.IsDBNull(3) ? null : r.GetInt32(3),
        TypeID             = r.IsDBNull(4) ? null : r.GetInt32(4),
        IsActive           = r.GetBoolean(5),
        CategoryName       = r.GetString(6),
        TypeName           = r.GetString(7),
        FullAccountString  = r.GetString(8),
        Seg1Value          = r.GetString(9),  Seg2Value = r.GetString(10), Seg3Value = r.GetString(11),
        Seg4Value          = r.GetString(12), Seg5Value = r.GetString(13), Seg6Value = r.GetString(14),
        CreatedAt          = r.GetDateTime(15),
    };
}
