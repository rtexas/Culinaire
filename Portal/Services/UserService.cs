using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class UserService
{
    private readonly string _connectionString;

    public UserService(string connectionString) => _connectionString = connectionString;

    // ── SELECT helpers ────────────────────────────────────────────────────────

    private const string SelectSql = """
        SELECT u.[UserID], u.[Username], u.[PasswordHash], u.[PasswordSalt], u.[FullName],
               u.[Email], u.[RoleType], u.[IsActive], u.[CreatedAt], u.[LastLoginAt],
               u.[Address1], u.[Address2], u.[Address3], u.[City],
               u.[StateRegionID], u.[PostalCode], u.[CountryID],
               sr.[Code], sr.[Name],
               c.[Code],  c.[Name]
        FROM   [dbo].[Users] u
        LEFT JOIN [dbo].[StatesRegions] sr ON sr.[StateRegionID] = u.[StateRegionID]
        LEFT JOIN [dbo].[Countries]      c ON c.[CountryID]      = u.[CountryID]
        """;

    private static User MapFull(SqlDataReader r) => new()
    {
        UserID        = r.GetInt32(0),
        Username      = r.GetString(1),
        PasswordHash  = r.GetString(2),
        PasswordSalt  = r.GetString(3),
        FullName      = r.GetString(4),
        Email         = r.IsDBNull(5)  ? null         : r.GetString(5),
        RoleType      = r.GetString(6),
        IsActive      = r.GetBoolean(7),
        CreatedAt     = r.GetDateTime(8),
        LastLoginAt   = r.IsDBNull(9)  ? null         : r.GetDateTime(9),
        Address1      = r.IsDBNull(10) ? null         : r.GetString(10),
        Address2      = r.IsDBNull(11) ? null         : r.GetString(11),
        Address3      = r.IsDBNull(12) ? null         : r.GetString(12),
        City          = r.IsDBNull(13) ? null         : r.GetString(13),
        StateRegionID = r.IsDBNull(14) ? null         : r.GetInt32(14),
        PostalCode    = r.IsDBNull(15) ? null         : r.GetString(15),
        CountryID     = r.IsDBNull(16) ? null         : r.GetInt32(16),
        StateCode     = r.IsDBNull(17) ? string.Empty : r.GetString(17),
        StateName     = r.IsDBNull(18) ? string.Empty : r.GetString(18),
        CountryCode   = r.IsDBNull(19) ? string.Empty : r.GetString(19),
        CountryName   = r.IsDBNull(20) ? string.Empty : r.GetString(20),
    };

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<List<User>> GetAllAsync(CancellationToken ct = default)
    {
        var list = new List<User>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(SelectSql + " ORDER BY u.[Username];", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) list.Add(MapFull(reader));
        return list;
    }

    public async Task<User?> GetByIdAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(SelectSql + " WHERE u.[UserID] = @UserID;", conn);
        cmd.Parameters.AddWithValue("@UserID", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapFull(reader) : null;
    }

    public async Task<bool> UsernameExistsAsync(string username, int excludeUserId = 0, CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(1) FROM [dbo].[Users] WHERE [Username]=@Username AND [UserID]<>@Exclude;";
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Username", username);
        cmd.Parameters.AddWithValue("@Exclude",  excludeUserId);
        return (int)(await cmd.ExecuteScalarAsync(ct))! > 0;
    }

    public async Task<int> CreateAsync(User user, string plainPassword, CancellationToken ct = default)
    {
        var salt = AuthService.GenerateSalt();
        var hash = AuthService.HashPassword(plainPassword, salt);
        const string sql = """
            INSERT INTO [dbo].[Users]
                ([Username],[PasswordHash],[PasswordSalt],[FullName],[Email],[RoleType],[IsActive],
                 [Address1],[Address2],[Address3],[City],[StateRegionID],[PostalCode],[CountryID])
            OUTPUT INSERTED.[UserID]
            VALUES (@Username,@Hash,@Salt,@FullName,@Email,@RoleType,@IsActive,
                    @Addr1,@Addr2,@Addr3,@City,@StateID,@Zip,@CtyID);
            """;
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        BindParams(cmd, user, hash, salt);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[Users]
            SET [FullName]=@FullName,[Email]=@Email,[RoleType]=@RoleType,[IsActive]=@IsActive,
                [Address1]=@Addr1,[Address2]=@Addr2,[Address3]=@Addr3,[City]=@City,
                [StateRegionID]=@StateID,[PostalCode]=@Zip,[CountryID]=@CtyID
            WHERE [UserID]=@UserID;
            """;
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserID",   user.UserID);
        cmd.Parameters.AddWithValue("@FullName", user.FullName);
        cmd.Parameters.AddWithValue("@Email",    (object?)user.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RoleType", user.RoleType);
        cmd.Parameters.AddWithValue("@IsActive", user.IsActive);
        BindAddress(cmd, user);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ChangePasswordAsync(int userId, string newPassword, CancellationToken ct = default)
    {
        var salt = AuthService.GenerateSalt();
        var hash = AuthService.HashPassword(newPassword, salt);
        const string sql = "UPDATE [dbo].[Users] SET [PasswordHash]=@Hash,[PasswordSalt]=@Salt WHERE [UserID]=@UserID;";
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserID", userId);
        cmd.Parameters.AddWithValue("@Hash",   hash);
        cmd.Parameters.AddWithValue("@Salt",   salt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> AnyUsersExistAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(1) FROM [dbo].[Users];";
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        return (int)(await cmd.ExecuteScalarAsync(ct))! > 0;
    }

    public async Task SeedAdminAsync(CancellationToken ct = default)
    {
        if (await AnyUsersExistAsync(ct)) return;
        var admin = new User { Username = "Administrator", FullName = "Administrator", RoleType = "Administrator", IsActive = true };
        await CreateAsync(admin, "P@ssw0rd124", ct);
    }

    // ── Import ────────────────────────────────────────────────────────────────

    public async Task<ImportResult> ImportAsync(Stream stream, string fileName, char delimiter = ',', CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;

        var states   = await LoadLookupAsync("SELECT [StateRegionID],[Code],[Name] FROM [dbo].[StatesRegions];", ct);
        var countries= await LoadLookupAsync("SELECT [CountryID],[Code],[Name] FROM [dbo].[Countries];", ct);

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        IAsyncEnumerable<Dictionary<string, string>> rows = ext is ".xlsx" or ".xls"
            ? ExcelRows(ms)
            : TextRows(ms, delimiter, ct);

        return await ProcessRowsAsync(rows, states, countries, ct);
    }

    private static async IAsyncEnumerable<Dictionary<string, string>> ExcelRows(Stream stream)
    {
        using var wb = new XLWorkbook(stream);
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
        IAsyncEnumerable<Dictionary<string, string>> rows,
        LookupMaps states, LookupMaps countries,
        CancellationToken ct)
    {
        var result = new ImportResult();
        int rowNum = 1;

        await foreach (var row in rows.WithCancellation(ct))
        {
            rowNum++;
            string G(string k) => row.TryGetValue(k, out var v) ? v.Trim() : string.Empty;

            var username = G("Username");
            if (string.IsNullOrWhiteSpace(username)) { result.RowsSkipped++; continue; }

            var fullName = G("FullName");
            if (string.IsNullOrWhiteSpace(fullName)) fullName = username;

            var roleType = G("RoleType");
            if (roleType is not ("Administrator" or "User" or "Viewer")) roleType = "User";

            var password = G("Password");

            try
            {
                var user = new User
                {
                    Username      = username,
                    FullName      = fullName,
                    Email         = Null(G("Email")),
                    RoleType      = roleType,
                    IsActive      = !G("IsActive").Equals("false", StringComparison.OrdinalIgnoreCase)
                                    && !G("IsActive").Equals("0", StringComparison.OrdinalIgnoreCase),
                    Address1      = Null(G("Address1")),
                    Address2      = Null(G("Address2")),
                    Address3      = Null(G("Address3")),
                    City          = Null(G("City")),
                    PostalCode    = Null(G("PostalCode")),
                    StateRegionID = ResolveId(states,   G("State"),   "State",   username, result),
                    CountryID     = ResolveId(countries, G("Country"), "Country", username, result),
                };
                await UpsertAsync(user, password, result, ct);
            }
            catch (Exception ex) { result.Errors.Add($"Row {rowNum}: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task UpsertAsync(User user, string password, ImportResult result, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var chk = new SqlCommand("SELECT [UserID] FROM [dbo].[Users] WHERE [Username]=@Username;", conn);
        chk.Parameters.AddWithValue("@Username", user.Username);
        var existing = await chk.ExecuteScalarAsync(ct);

        if (existing is int id)
        {
            // Update profile and address; never change password unless one is supplied
            const string upd = """
                UPDATE [dbo].[Users]
                SET [FullName]=@FullName,[Email]=@Email,[RoleType]=@RoleType,[IsActive]=@IsActive,
                    [Address1]=@Addr1,[Address2]=@Addr2,[Address3]=@Addr3,[City]=@City,
                    [StateRegionID]=@StateID,[PostalCode]=@Zip,[CountryID]=@CtyID
                WHERE [UserID]=@UserID;
                """;
            await using var cmd = new SqlCommand(upd, conn);
            cmd.Parameters.AddWithValue("@UserID",   id);
            cmd.Parameters.AddWithValue("@FullName", user.FullName);
            cmd.Parameters.AddWithValue("@Email",    (object?)user.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RoleType", user.RoleType);
            cmd.Parameters.AddWithValue("@IsActive", user.IsActive);
            BindAddress(cmd, user);
            await cmd.ExecuteNonQueryAsync(ct);

            if (!string.IsNullOrWhiteSpace(password))
                await ChangePasswordAsync(id, password, ct);

            result.AccountsUpdated++;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(password)) password = "P@ssw0rd1!";
            var salt = AuthService.GenerateSalt();
            var hash = AuthService.HashPassword(password, salt);
            const string ins = """
                INSERT INTO [dbo].[Users]
                    ([Username],[PasswordHash],[PasswordSalt],[FullName],[Email],[RoleType],[IsActive],
                     [Address1],[Address2],[Address3],[City],[StateRegionID],[PostalCode],[CountryID])
                VALUES (@Username,@Hash,@Salt,@FullName,@Email,@RoleType,@IsActive,
                        @Addr1,@Addr2,@Addr3,@City,@StateID,@Zip,@CtyID);
                """;
            await using var cmd = new SqlCommand(ins, conn);
            BindParams(cmd, user, hash, salt);
            await cmd.ExecuteNonQueryAsync(ct);
            result.AccountsCreated++;
        }
    }

    // ── Lookup helpers ────────────────────────────────────────────────────────

    private sealed record LookupMaps(Dictionary<string, int> ByCode, Dictionary<string, int> ByName);

    private async Task<LookupMaps> LoadLookupAsync(string sql, CancellationToken ct)
    {
        var byCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            int id   = reader.GetInt32(0);
            var code = reader.GetString(1);
            var name = reader.GetString(2);
            byCode.TryAdd(code, id);
            byName.TryAdd(name, id);
        }
        return new LookupMaps(byCode, byName);
    }

    private static int? ResolveId(LookupMaps maps, string value, string field, string username, ImportResult result)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (maps.ByCode.TryGetValue(value, out var id)) return id;
        if (maps.ByName.TryGetValue(value, out id))     return id;
        result.Errors.Add($"User '{username}': {field} '{value}' not found — left blank.");
        return null;
    }

    // ── Param helpers ─────────────────────────────────────────────────────────

    private static void BindParams(SqlCommand cmd, User u, string hash, string salt)
    {
        cmd.Parameters.AddWithValue("@Username", u.Username.Trim());
        cmd.Parameters.AddWithValue("@Hash",     hash);
        cmd.Parameters.AddWithValue("@Salt",     salt);
        cmd.Parameters.AddWithValue("@FullName", u.FullName.Trim());
        cmd.Parameters.AddWithValue("@Email",    (object?)u.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RoleType", u.RoleType);
        cmd.Parameters.AddWithValue("@IsActive", u.IsActive);
        BindAddress(cmd, u);
    }

    private static void BindAddress(SqlCommand cmd, User u)
    {
        cmd.Parameters.AddWithValue("@Addr1",   (object?)u.Address1   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Addr2",   (object?)u.Address2   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Addr3",   (object?)u.Address3   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@City",    (object?)u.City       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StateID", (object?)u.StateRegionID ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Zip",     (object?)u.PostalCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CtyID",   (object?)u.CountryID  ?? DBNull.Value);
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
}
