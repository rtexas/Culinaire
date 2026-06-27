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
            ? ImportHelper.ExcelRowsAsync(ms)
            : ImportHelper.TextRowsAsync(ms, delimiter);

        return await ProcessRowsAsync(rows, states, countries, accounts, ct);
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
        await using var cmd = new SqlCommand(
            "SELECT [AccountID],[AccountName],ISNULL([FullAccountString],'') FROM [dbo].[ChartOfAccounts];", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id   = reader.GetInt32(0);
            map.TryAdd(reader.GetString(1), id);          // match by AccountName
            var full = reader.GetString(2);
            if (!string.IsNullOrEmpty(full)) map.TryAdd(full, id); // match by FullAccountString
        }
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
        cmd.Parameters.AddWithValue("@Desc",    (object?)ImportHelper.NullIfEmpty(v.Description) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Addr1",   (object?)ImportHelper.NullIfEmpty(v.Address1)    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Addr2",   (object?)ImportHelper.NullIfEmpty(v.Address2)    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Addr3",   (object?)ImportHelper.NullIfEmpty(v.Address3)    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@City",    (object?)ImportHelper.NullIfEmpty(v.City)        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StateID", (object?)v.StateRegionID     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Zip",     (object?)ImportHelper.NullIfEmpty(v.PostalCode)  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CtyID",   (object?)v.CountryID         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AcctID",  (object?)v.DefaultPayablesAccountID ?? DBNull.Value);
    }

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
