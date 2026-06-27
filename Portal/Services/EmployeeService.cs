using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class EmployeeService
{
    private readonly string _cs;
    public EmployeeService(string connectionString) => _cs = connectionString;

    public async Task<List<Employee>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT e.[EmployeeID], e.[ExternalID], e.[Name], e.[Description], e.[IsActive], e.[CreatedAt],
                   ISNULL(STRING_AGG(CAST(el.[LocationID] AS NVARCHAR(10)), ','),'') AS Locs
            FROM   [dbo].[Employees] e
            LEFT JOIN [dbo].[EmployeeLocations] el ON el.[EmployeeID] = e.[EmployeeID]
            GROUP  BY e.[EmployeeID], e.[ExternalID], e.[Name], e.[Description], e.[IsActive], e.[CreatedAt]
            ORDER  BY e.[Name];
            """;
        var list = new List<Employee>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(Map(r));
        return list;
    }

    public async Task<Employee?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT e.[EmployeeID], e.[ExternalID], e.[Name], e.[Description], e.[IsActive], e.[CreatedAt],
                   ISNULL(STRING_AGG(CAST(el.[LocationID] AS NVARCHAR(10)), ','),'') AS Locs
            FROM   [dbo].[Employees] e
            LEFT JOIN [dbo].[EmployeeLocations] el ON el.[EmployeeID] = e.[EmployeeID]
            WHERE  e.[EmployeeID] = @ID
            GROUP  BY e.[EmployeeID], e.[ExternalID], e.[Name], e.[Description], e.[IsActive], e.[CreatedAt];
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Map(r) : null;
    }

    public async Task<int> CreateAsync(Employee e, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [dbo].[Employees]([ExternalID],[Name],[Description],[IsActive])
            OUTPUT INSERTED.[EmployeeID]
            VALUES(@ExtID,@Name,@Desc,@Active);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        BindCore(cmd, e);
        var newId = (int)(await cmd.ExecuteScalarAsync(ct))!;
        await SaveLocationsAsync(conn, newId, e.LocationIDs, ct);
        return newId;
    }

    public async Task UpdateAsync(Employee e, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[Employees]
            SET [ExternalID]=@ExtID,[Name]=@Name,[Description]=@Desc,[IsActive]=@Active
            WHERE [EmployeeID]=@ID;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", e.EmployeeID);
        BindCore(cmd, e);
        await cmd.ExecuteNonQueryAsync(ct);
        await SaveLocationsAsync(conn, e.EmployeeID, e.LocationIDs, ct);
    }

    public async Task SetActiveAsync(int id, bool active, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "UPDATE [dbo].[Employees] SET [IsActive]=@A WHERE [EmployeeID]=@ID;", conn);
        cmd.Parameters.AddWithValue("@A",  active);
        cmd.Parameters.AddWithValue("@ID", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task SaveLocationsAsync(SqlConnection conn, int empId, List<int> locationIds, CancellationToken ct)
    {
        await using var del = new SqlCommand(
            "DELETE FROM [dbo].[EmployeeLocations] WHERE [EmployeeID]=@ID;", conn);
        del.Parameters.AddWithValue("@ID", empId);
        await del.ExecuteNonQueryAsync(ct);

        foreach (var locId in locationIds)
        {
            await using var ins = new SqlCommand(
                "INSERT INTO [dbo].[EmployeeLocations]([EmployeeID],[LocationID]) VALUES(@E,@L);", conn);
            ins.Parameters.AddWithValue("@E", empId);
            ins.Parameters.AddWithValue("@L", locId);
            await ins.ExecuteNonQueryAsync(ct);
        }
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

        // Collect all rows grouped by ExternalID
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        var groups = new Dictionary<string, (string Name, string Desc, bool Active, List<string> LocCodes)>(StringComparer.OrdinalIgnoreCase);
        for (int row = 2; row <= lastRow; row++)
        {
            string Get(string key) => headers.TryGetValue(key, out var col) ? ws.Cell(row, col).GetString().Trim() : string.Empty;
            var extId = Get("ExternalID");
            if (string.IsNullOrWhiteSpace(extId)) { result.RowsSkipped++; continue; }
            var locCode = Get("LocationCode");
            if (!groups.TryGetValue(extId, out var g))
            {
                bool active = Get("IsActive") is "" or "1" or "true" or "yes" or "True" or "Yes";
                g = (Get("Name"), Get("Description"), active, []);
                groups[extId] = g;
            }
            if (!string.IsNullOrWhiteSpace(locCode)) g.LocCodes.Add(locCode);
        }
        await ProcessGroupsAsync(groups, result, ct);
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

        var groups = new Dictionary<string, (string Name, string Desc, bool Active, List<string> LocCodes)>(StringComparer.OrdinalIgnoreCase);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ImportHelper.ParseLine(line, delimiter);
            string Get(string key) => headers.TryGetValue(key, out var idx) && idx < cols.Length ? cols[idx].Trim() : string.Empty;
            var extId = Get("ExternalID");
            if (string.IsNullOrWhiteSpace(extId)) { result.RowsSkipped++; continue; }
            var locCode = Get("LocationCode");
            if (!groups.TryGetValue(extId, out var g))
            {
                bool active = Get("IsActive") is "" or "1" or "true" or "yes" or "True" or "Yes";
                g = (Get("Name"), Get("Description"), active, []);
                groups[extId] = g;
            }
            if (!string.IsNullOrWhiteSpace(locCode)) g.LocCodes.Add(locCode);
        }
        await ProcessGroupsAsync(groups, result, ct);
        return result;
    }

    private async Task ProcessGroupsAsync(
        Dictionary<string, (string Name, string Desc, bool Active, List<string> LocCodes)> groups,
        ImportResult result, CancellationToken ct)
    {
        foreach (var (extId, g) in groups)
        {
            if (string.IsNullOrWhiteSpace(g.Name)) { result.Errors.Add($"ExternalID '{extId}': Name is required."); result.RowsSkipped++; continue; }
            try
            {
                await using var conn = new SqlConnection(_cs);
                await conn.OpenAsync(ct);

                // Resolve location codes to IDs
                var locIds = new List<int>();
                foreach (var code in g.LocCodes.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    await using var locCmd = new SqlCommand("SELECT [LocationID] FROM [dbo].[Locations] WHERE [Code]=@C;", conn);
                    locCmd.Parameters.AddWithValue("@C", code.ToUpperInvariant());
                    var locId = await locCmd.ExecuteScalarAsync(ct);
                    if (locId is int lid) locIds.Add(lid);
                    else result.Errors.Add($"ExternalID '{extId}': location code '{code}' not found — skipped.");
                }

                // Upsert employee
                await using var chkCmd = new SqlCommand("SELECT [EmployeeID] FROM [dbo].[Employees] WHERE [ExternalID]=@E;", conn);
                chkCmd.Parameters.AddWithValue("@E", extId);
                var existing = await chkCmd.ExecuteScalarAsync(ct);
                int empId;
                if (existing is int eid)
                {
                    await using var upd = new SqlCommand(
                        "UPDATE [dbo].[Employees] SET [Name]=@N,[Description]=@D,[IsActive]=@A WHERE [EmployeeID]=@ID;", conn);
                    upd.Parameters.AddWithValue("@ID", eid);
                    upd.Parameters.AddWithValue("@N",  g.Name);
                    upd.Parameters.AddWithValue("@D",  string.IsNullOrWhiteSpace(g.Desc) ? DBNull.Value : g.Desc);
                    upd.Parameters.AddWithValue("@A",  g.Active);
                    await upd.ExecuteNonQueryAsync(ct);
                    empId = eid;
                    result.AccountsUpdated++;
                }
                else
                {
                    await using var ins = new SqlCommand(
                        "INSERT INTO [dbo].[Employees]([ExternalID],[Name],[Description],[IsActive]) OUTPUT INSERTED.[EmployeeID] VALUES(@E,@N,@D,@A);", conn);
                    ins.Parameters.AddWithValue("@E", extId);
                    ins.Parameters.AddWithValue("@N", g.Name);
                    ins.Parameters.AddWithValue("@D", string.IsNullOrWhiteSpace(g.Desc) ? DBNull.Value : g.Desc);
                    ins.Parameters.AddWithValue("@A", g.Active);
                    empId = (int)(await ins.ExecuteScalarAsync(ct))!;
                    result.AccountsCreated++;
                }

                await SaveLocationsAsync(conn, empId, locIds, ct);
            }
            catch (Exception ex) { result.Errors.Add($"ExternalID '{extId}': {ex.Message}"); result.RowsSkipped++; }
        }
    }

    private static void BindCore(SqlCommand cmd, Employee e)
    {
        cmd.Parameters.AddWithValue("@ExtID",  e.ExternalID);
        cmd.Parameters.AddWithValue("@Name",   e.Name);
        cmd.Parameters.AddWithValue("@Desc",   string.IsNullOrWhiteSpace(e.Description) ? DBNull.Value : e.Description);
        cmd.Parameters.AddWithValue("@Active", e.IsActive);
    }

    private static Employee Map(SqlDataReader r)
    {
        var locStr = r.GetString(6);
        return new Employee
        {
            EmployeeID  = r.GetInt32(0),
            ExternalID  = r.GetString(1),
            Name        = r.GetString(2),
            Description = r.IsDBNull(3) ? string.Empty : r.GetString(3),
            IsActive    = r.GetBoolean(4),
            CreatedAt   = r.GetDateTime(5),
            LocationIDs = string.IsNullOrEmpty(locStr)
                ? []
                : locStr.Split(',').Select(int.Parse).ToList(),
        };
    }
}
