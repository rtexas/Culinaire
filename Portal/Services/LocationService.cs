using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class LocationService
{
    private readonly string _cs;
    public LocationService(string connectionString) => _cs = connectionString;

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<List<Location>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT [LocationID],[Code],[Name],[Description],[SegmentNumber],[IsActive],[CreatedAt],
                   ISNULL([Address1],''), ISNULL([Address2],''), ISNULL([City],''), ISNULL([State],''), ISNULL([Zip],'')
            FROM   [dbo].[Locations]
            ORDER  BY [Code];
            """;
        var list = new List<Location>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Map(reader));
        return list;
    }

    public async Task<Location?> GetByIdAsync(int locationId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT [LocationID],[Code],[Name],[Description],[SegmentNumber],[IsActive],[CreatedAt],
                   ISNULL([Address1],''), ISNULL([Address2],''), ISNULL([City],''), ISNULL([State],''), ISNULL([Zip],'')
            FROM   [dbo].[Locations]
            WHERE  [LocationID] = @ID;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", locationId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return Map(reader);
        return null;
    }

    public async Task<List<Location>> GetForUserAsync(int userId, string roleType, CancellationToken ct = default)
    {
        if (roleType == "Administrator") return await GetAllAsync(ct);

        const string sql = """
            SELECT l.[LocationID], l.[Code], l.[Name], l.[Description], l.[SegmentNumber], l.[IsActive], l.[CreatedAt],
                   ISNULL(l.[Address1],''), ISNULL(l.[Address2],''), ISNULL(l.[City],''), ISNULL(l.[State],''), ISNULL(l.[Zip],'')
            FROM   [dbo].[Locations]      l
            JOIN   [dbo].[UserLocations]  ul ON ul.[LocationID] = l.[LocationID]
            WHERE  ul.[UserID] = @UserID AND l.[IsActive] = 1
            ORDER  BY l.[Code];
            """;
        var list = new List<Location>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserID", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Map(reader));
        return list;
    }

    public async Task<int> CreateAsync(Location item, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [dbo].[Locations]([Code],[Name],[Description],[SegmentNumber],[Address1],[Address2],[City],[State],[Zip])
            OUTPUT INSERTED.[LocationID]
            VALUES(@Code,@Name,@Desc,@Seg,@Addr1,@Addr2,@City,@State,@Zip);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Code",  item.Code.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@Name",  item.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc",  (object?)NullIfEmpty(item.Description) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Seg",   item.SegmentNumber);
        cmd.Parameters.AddWithValue("@Addr1", (object?)NullIfEmpty(item.Address1) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Addr2", (object?)NullIfEmpty(item.Address2) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@City",  (object?)NullIfEmpty(item.City)     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@State", (object?)NullIfEmpty(item.State)    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Zip",   (object?)NullIfEmpty(item.Zip)      ?? DBNull.Value);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateAsync(Location item, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[Locations]
            SET [Code]=@Code,[Name]=@Name,[Description]=@Desc,[SegmentNumber]=@Seg,
                [Address1]=@Addr1,[Address2]=@Addr2,[City]=@City,[State]=@State,[Zip]=@Zip
            WHERE [LocationID]=@ID;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID",    item.LocationID);
        cmd.Parameters.AddWithValue("@Code",  item.Code.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@Name",  item.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc",  (object?)NullIfEmpty(item.Description) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Seg",   item.SegmentNumber);
        cmd.Parameters.AddWithValue("@Addr1", (object?)NullIfEmpty(item.Address1) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Addr2", (object?)NullIfEmpty(item.Address2) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@City",  (object?)NullIfEmpty(item.City)     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@State", (object?)NullIfEmpty(item.State)    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Zip",   (object?)NullIfEmpty(item.Zip)      ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM [dbo].[Locations] WHERE [LocationID]=@ID;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── User-location assignments ─────────────────────────────────────────────

    public async Task<List<int>> GetUserLocationIDsAsync(int userId, CancellationToken ct = default)
    {
        const string sql = "SELECT [LocationID] FROM [dbo].[UserLocations] WHERE [UserID]=@UserID;";
        var list = new List<int>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserID", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(reader.GetInt32(0));
        return list;
    }

    public async Task SetUserLocationsAsync(int userId, List<int> locationIds, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            await using var del = new SqlCommand(
                "DELETE FROM [dbo].[UserLocations] WHERE [UserID]=@UserID;", conn, (SqlTransaction)tx);
            del.Parameters.AddWithValue("@UserID", userId);
            await del.ExecuteNonQueryAsync(ct);

            foreach (var lid in locationIds)
            {
                await using var ins = new SqlCommand(
                    "INSERT INTO [dbo].[UserLocations]([UserID],[LocationID]) VALUES(@UID,@LID);",
                    conn, (SqlTransaction)tx);
                ins.Parameters.AddWithValue("@UID", userId);
                ins.Parameters.AddWithValue("@LID", lid);
                await ins.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
        catch { await tx.RollbackAsync(ct); throw; }
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
        var ws      = wb.Worksheets.First();

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
            int    seg = int.TryParse(Get("Segment Number"), out var s) ? s : 0;
            try
            {
                await UpsertRowAsync(Get("Code"), Get("Name"), Get("Description"), seg,
                    Get("Address1"), Get("Address2"), Get("City"), Get("State"), Get("Zip"),
                    result, ct);
            }
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

        int lineNum = 1;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNum++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ParseLine(line, delimiter);
            string Get(string key) => headers.TryGetValue(key, out var idx) && idx < cols.Length ? cols[idx].Trim() : string.Empty;
            int    seg = int.TryParse(Get("Segment Number"), out var s) ? s : 0;
            try
            {
                await UpsertRowAsync(Get("Code"), Get("Name"), Get("Description"), seg,
                    Get("Address1"), Get("Address2"), Get("City"), Get("State"), Get("Zip"),
                    result, ct);
            }
            catch (Exception ex) { result.Errors.Add($"Line {lineNum}: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task UpsertRowAsync(string code, string name, string description, int segmentNumber,
        string address1, string address2, string city, string state, string zip,
        ImportResult result, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name)) { result.RowsSkipped++; return; }

        code = code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) { result.Errors.Add($"Row for '{name}' skipped — Code is required."); result.RowsSkipped++; return; }

        const string checkSql = "SELECT [LocationID] FROM [dbo].[Locations] WHERE [Code]=@Code;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var checkCmd = new SqlCommand(checkSql, conn);
        checkCmd.Parameters.AddWithValue("@Code", code);
        var existing = await checkCmd.ExecuteScalarAsync(ct);

        if (existing is int existingId)
        {
            const string upd = """
                UPDATE [dbo].[Locations]
                SET [Name]=@Name,[Description]=@Desc,[SegmentNumber]=@Seg,
                    [Address1]=@Addr1,[Address2]=@Addr2,[City]=@City,[State]=@State,[Zip]=@Zip
                WHERE [LocationID]=@ID;
                """;
            await using var updCmd = new SqlCommand(upd, conn);
            updCmd.Parameters.AddWithValue("@ID",    existingId);
            updCmd.Parameters.AddWithValue("@Name",  name);
            updCmd.Parameters.AddWithValue("@Desc",  (object?)NullIfEmpty(description) ?? DBNull.Value);
            updCmd.Parameters.AddWithValue("@Seg",   segmentNumber);
            updCmd.Parameters.AddWithValue("@Addr1", (object?)NullIfEmpty(address1) ?? DBNull.Value);
            updCmd.Parameters.AddWithValue("@Addr2", (object?)NullIfEmpty(address2) ?? DBNull.Value);
            updCmd.Parameters.AddWithValue("@City",  (object?)NullIfEmpty(city)     ?? DBNull.Value);
            updCmd.Parameters.AddWithValue("@State", (object?)NullIfEmpty(state)    ?? DBNull.Value);
            updCmd.Parameters.AddWithValue("@Zip",   (object?)NullIfEmpty(zip)      ?? DBNull.Value);
            await updCmd.ExecuteNonQueryAsync(ct);
            result.AccountsUpdated++;
        }
        else
        {
            const string ins = """
                INSERT INTO [dbo].[Locations]([Code],[Name],[Description],[SegmentNumber],[Address1],[Address2],[City],[State],[Zip])
                VALUES(@Code,@Name,@Desc,@Seg,@Addr1,@Addr2,@City,@State,@Zip);
                """;
            await using var insCmd = new SqlCommand(ins, conn);
            insCmd.Parameters.AddWithValue("@Code",  code);
            insCmd.Parameters.AddWithValue("@Name",  name);
            insCmd.Parameters.AddWithValue("@Desc",  (object?)NullIfEmpty(description) ?? DBNull.Value);
            insCmd.Parameters.AddWithValue("@Seg",   segmentNumber);
            insCmd.Parameters.AddWithValue("@Addr1", (object?)NullIfEmpty(address1) ?? DBNull.Value);
            insCmd.Parameters.AddWithValue("@Addr2", (object?)NullIfEmpty(address2) ?? DBNull.Value);
            insCmd.Parameters.AddWithValue("@City",  (object?)NullIfEmpty(city)     ?? DBNull.Value);
            insCmd.Parameters.AddWithValue("@State", (object?)NullIfEmpty(state)    ?? DBNull.Value);
            insCmd.Parameters.AddWithValue("@Zip",   (object?)NullIfEmpty(zip)      ?? DBNull.Value);
            await insCmd.ExecuteNonQueryAsync(ct);
            result.AccountsCreated++;
        }
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
            else if (c == delimiter && !inQ) { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return [.. fields];
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static Location Map(SqlDataReader r) => new()
    {
        LocationID    = r.GetInt32(0),
        Code          = r.GetString(1),
        Name          = r.GetString(2),
        Description   = r.IsDBNull(3) ? string.Empty : r.GetString(3),
        SegmentNumber = r.GetInt32(4),
        IsActive      = r.GetBoolean(5),
        CreatedAt     = r.GetDateTime(6),
        Address1      = r.IsDBNull(7)  ? string.Empty : r.GetString(7),
        Address2      = r.IsDBNull(8)  ? string.Empty : r.GetString(8),
        City          = r.IsDBNull(9)  ? string.Empty : r.GetString(9),
        State         = r.IsDBNull(10) ? string.Empty : r.GetString(10),
        Zip           = r.IsDBNull(11) ? string.Empty : r.GetString(11),
    };
}
