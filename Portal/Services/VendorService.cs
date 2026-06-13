using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class VendorService
{
    private readonly string _cs;
    public VendorService(string connectionString) => _cs = connectionString;

    // ── CRUD ─────────────────────────────────────────────────────────────────

    private const string SelectSql = """
        SELECT v.[VendorID], v.[VendorCode], v.[Name], v.[Description],
               v.[Address1], v.[Address2], v.[Address3], v.[City],
               v.[StateRegionID], v.[PostalCode], v.[CountryID],
               v.[DefaultPayablesAccountID], v.[IsActive], v.[CreatedAt],
               sr.[Code], sr.[Name],
               c.[Code],  c.[Name],
               a.[AccountName]
        FROM   [dbo].[Vendors] v
        LEFT JOIN [dbo].[StatesRegions]  sr ON sr.[StateRegionID] = v.[StateRegionID]
        LEFT JOIN [dbo].[Countries]       c ON c.[CountryID]      = v.[CountryID]
        LEFT JOIN [dbo].[ChartOfAccounts] a ON a.[AccountID]      = v.[DefaultPayablesAccountID]
        """;

    public async Task<List<Vendor>> GetAllAsync(CancellationToken ct = default)
    {
        var list = new List<Vendor>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(SelectSql + " ORDER BY v.[Name];", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Map(reader));
        return list;
    }

    public async Task<int> CreateAsync(Vendor item, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [dbo].[Vendors]
                ([VendorCode],[Name],[Description],[Address1],[Address2],[Address3],
                 [City],[StateRegionID],[PostalCode],[CountryID],[DefaultPayablesAccountID])
            OUTPUT INSERTED.[VendorID]
            VALUES(@Code,@Name,@Desc,@Addr1,@Addr2,@Addr3,@City,@StateID,@Zip,@CtyID,@AcctID);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        BindParams(cmd, item);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateAsync(Vendor item, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[Vendors]
            SET [VendorCode]=@Code,[Name]=@Name,[Description]=@Desc,
                [Address1]=@Addr1,[Address2]=@Addr2,[Address3]=@Addr3,
                [City]=@City,[StateRegionID]=@StateID,[PostalCode]=@Zip,
                [CountryID]=@CtyID,[DefaultPayablesAccountID]=@AcctID
            WHERE [VendorID]=@VendorID;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@VendorID", item.VendorID);
        BindParams(cmd, item);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("DELETE FROM [dbo].[Vendors] WHERE [VendorID]=@ID;", conn);
        cmd.Parameters.AddWithValue("@ID", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Import ────────────────────────────────────────────────────────────────

    public async Task<ImportResult> ImportAsync(Stream stream, string fileName, char delimiter = ',', CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;

        var states   = await LoadLookupAsync("SELECT [StateRegionID],[Code],[Name] FROM [dbo].[StatesRegions];", ct);
        var countries= await LoadLookupAsync("SELECT [CountryID],[Code],[Name] FROM [dbo].[Countries];", ct);
        var accounts = await LoadAccountLookupAsync(ct);

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        IAsyncEnumerable<Dictionary<string,string>> rows = ext is ".xlsx" or ".xls"
            ? ExcelRows(ms)
            : TextRows(ms, delimiter);

        return await ProcessRowsAsync(rows, states, countries, accounts, ct);
    }

    private static async IAsyncEnumerable<Dictionary<string,string>> ExcelRows(Stream stream)
    {
        using var wb    = new XLWorkbook(stream);
        var ws          = wb.Worksheets.First();
        var headers     = BuildHeaderMap(ws);
        int lastRow     = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (int row = 2; row <= lastRow; row++)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, col) in headers)
                d[key] = ws.Cell(row, col).GetString().Trim();
            yield return d;
            await Task.CompletedTask;
        }
    }

    private static async IAsyncEnumerable<Dictionary<string,string>> TextRows(
        Stream stream, char delimiter,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
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

    private async Task<ImportResult> ProcessRowsAsync(
        IAsyncEnumerable<Dictionary<string,string>> rows,
        LookupMaps states, LookupMaps countries, Dictionary<string,int> accounts,
        CancellationToken ct)
    {
        var result = new ImportResult();
        int rowNum = 1;
        await foreach (var row in rows.WithCancellation(ct))
        {
            rowNum++;
            string G(string k) => row.TryGetValue(k, out var v) ? v.Trim() : string.Empty;
            var code = G("VendorCode").ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code)) { result.RowsSkipped++; continue; }

            try
            {
                var vendor = new Vendor
                {
                    VendorCode               = code,
                    Name                     = G("Name"),
                    Description              = G("Description"),
                    Address1                 = G("Address1"),
                    Address2                 = G("Address2"),
                    Address3                 = G("Address3"),
                    City                     = G("City"),
                    PostalCode               = G("PostalCode"),
                    StateRegionID            = ResolveId(states,    G("State"),                  "State",   code, result),
                    CountryID                = ResolveId(countries,  G("Country"),                "Country", code, result),
                    DefaultPayablesAccountID = ResolveAccount(accounts, G("DefaultPayablesAccount"), code, result),
                };
                await UpsertAsync(vendor, result, ct);
            }
            catch (Exception ex) { result.Errors.Add($"Row {rowNum}: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task UpsertAsync(Vendor item, ImportResult result, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var chk = new SqlCommand("SELECT [VendorID] FROM [dbo].[Vendors] WHERE [VendorCode]=@Code;", conn);
        chk.Parameters.AddWithValue("@Code", item.VendorCode);
        var existing = await chk.ExecuteScalarAsync(ct);

        if (existing is int id)
        {
            const string upd = """
                UPDATE [dbo].[Vendors]
                SET [Name]=@Name,[Description]=@Desc,[Address1]=@Addr1,[Address2]=@Addr2,
                    [Address3]=@Addr3,[City]=@City,[StateRegionID]=@StateID,[PostalCode]=@Zip,
                    [CountryID]=@CtyID,[DefaultPayablesAccountID]=@AcctID
                WHERE [VendorID]=@VendorID;
                """;
            await using var cmd = new SqlCommand(upd, conn);
            cmd.Parameters.AddWithValue("@VendorID", id);
            BindParams(cmd, item);
            await cmd.ExecuteNonQueryAsync(ct);
            result.AccountsUpdated++;
        }
        else
        {
            const string ins = """
                INSERT INTO [dbo].[Vendors]
                    ([VendorCode],[Name],[Description],[Address1],[Address2],[Address3],
                     [City],[StateRegionID],[PostalCode],[CountryID],[DefaultPayablesAccountID])
                VALUES(@Code,@Name,@Desc,@Addr1,@Addr2,@Addr3,@City,@StateID,@Zip,@CtyID,@AcctID);
                """;
            await using var cmd = new SqlCommand(ins, conn);
            BindParams(cmd, item);
            await cmd.ExecuteNonQueryAsync(ct);
            result.AccountsCreated++;
        }
    }

    // ── Lookup helpers ────────────────────────────────────────────────────────

    private sealed record LookupMaps(
        Dictionary<string,int> ByCode,
        Dictionary<string,int> ByName);

    private async Task<LookupMaps> LoadLookupAsync(string sql, CancellationToken ct)
    {
        var byCode = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        var byName = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            int  id   = reader.GetInt32(0);
            var  code = reader.GetString(1);
            var  name = reader.GetString(2);
            byCode.TryAdd(code, id);
            byName.TryAdd(name, id);
        }
        return new LookupMaps(byCode, byName);
    }

    private async Task<Dictionary<string,int>> LoadAccountLookupAsync(CancellationToken ct)
    {
        var map = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand("SELECT [AccountID],[AccountName] FROM [dbo].[ChartOfAccounts];", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            map.TryAdd(reader.GetString(1), reader.GetInt32(0));
        return map;
    }

    private static int? ResolveId(LookupMaps maps, string value, string field, string vendorCode, ImportResult result)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (maps.ByCode.TryGetValue(value, out var id)) return id;
        if (maps.ByName.TryGetValue(value, out id))     return id;
        result.Errors.Add($"Vendor '{vendorCode}': {field} '{value}' not found — left blank.");
        return null;
    }

    private static int? ResolveAccount(Dictionary<string,int> accounts, string value, string vendorCode, ImportResult result)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (accounts.TryGetValue(value, out var id)) return id;
        result.Errors.Add($"Vendor '{vendorCode}': Payables account '{value}' not found — left blank.");
        return null;
    }

    // ── Param / map helpers ───────────────────────────────────────────────────

    private static void BindParams(SqlCommand cmd, Vendor v)
    {
        cmd.Parameters.AddWithValue("@Code",    v.VendorCode.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@Name",    v.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc",    (object?)Null(v.Description) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Addr1",   (object?)Null(v.Address1)    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Addr2",   (object?)Null(v.Address2)    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Addr3",   (object?)Null(v.Address3)    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@City",    (object?)Null(v.City)        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StateID", (object?)v.StateRegionID     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Zip",     (object?)Null(v.PostalCode)  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CtyID",   (object?)v.CountryID         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AcctID",  (object?)v.DefaultPayablesAccountID ?? DBNull.Value);
    }

    private static Dictionary<string,int> BuildHeaderMap(IXLWorksheet ws)
    {
        var map = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
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

    private static Vendor Map(SqlDataReader r) => new()
    {
        VendorID                 = r.GetInt32(0),
        VendorCode               = r.GetString(1),
        Name                     = r.GetString(2),
        Description              = r.IsDBNull(3)  ? string.Empty : r.GetString(3),
        Address1                 = r.IsDBNull(4)  ? string.Empty : r.GetString(4),
        Address2                 = r.IsDBNull(5)  ? string.Empty : r.GetString(5),
        Address3                 = r.IsDBNull(6)  ? string.Empty : r.GetString(6),
        City                     = r.IsDBNull(7)  ? string.Empty : r.GetString(7),
        StateRegionID            = r.IsDBNull(8)  ? null : r.GetInt32(8),
        PostalCode               = r.IsDBNull(9)  ? string.Empty : r.GetString(9),
        CountryID                = r.IsDBNull(10) ? null : r.GetInt32(10),
        DefaultPayablesAccountID = r.IsDBNull(11) ? null : r.GetInt32(11),
        IsActive                 = r.GetBoolean(12),
        CreatedAt                = r.GetDateTime(13),
        StateCode                = r.IsDBNull(14) ? string.Empty : r.GetString(14),
        StateName                = r.IsDBNull(15) ? string.Empty : r.GetString(15),
        CountryCode              = r.IsDBNull(16) ? string.Empty : r.GetString(16),
        CountryName              = r.IsDBNull(17) ? string.Empty : r.GetString(17),
        PayablesAccount          = r.IsDBNull(18) ? string.Empty : r.GetString(18),
    };
}
