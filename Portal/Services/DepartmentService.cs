using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class DepartmentService
{
    private readonly string _cs;
    public DepartmentService(string connectionString) => _cs = connectionString;

    public async Task<List<Department>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT d.[DepartmentID], d.[Code], d.[Name], d.[Description], d.[IsActive], d.[CreatedAt],
                   ISNULL(STRING_AGG(CAST(ld.[LocationID] AS NVARCHAR(10)),','),'') AS Locs
            FROM   [dbo].[Departments] d
            LEFT JOIN [dbo].[LocationDepartments] ld ON ld.[DepartmentID] = d.[DepartmentID]
            GROUP  BY d.[DepartmentID], d.[Code], d.[Name], d.[Description], d.[IsActive], d.[CreatedAt]
            ORDER  BY d.[Code];
            """;
        var list = new List<Department>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(Map(r));
        return list;
    }

    public async Task<List<Department>> GetForLocationAsync(int locationId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT d.[DepartmentID], d.[Code], d.[Name], d.[Description], d.[IsActive], d.[CreatedAt],
                   ISNULL(STRING_AGG(CAST(ld2.[LocationID] AS NVARCHAR(10)),','),'') AS Locs
            FROM   [dbo].[Departments] d
            JOIN   [dbo].[LocationDepartments] ld  ON ld.[DepartmentID]  = d.[DepartmentID] AND ld.[LocationID] = @L
            LEFT JOIN [dbo].[LocationDepartments] ld2 ON ld2.[DepartmentID] = d.[DepartmentID]
            WHERE  d.[IsActive] = 1
            GROUP  BY d.[DepartmentID], d.[Code], d.[Name], d.[Description], d.[IsActive], d.[CreatedAt]
            ORDER  BY d.[Code];
            """;
        var list = new List<Department>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@L", locationId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(Map(r));
        return list;
    }

    public async Task<int> CreateAsync(Department d, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [dbo].[Departments]([Code],[Name],[Description],[IsActive])
            OUTPUT INSERTED.[DepartmentID]
            VALUES(@Code,@Name,@Desc,@Active);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        BindCore(cmd, d);
        var newId = (int)(await cmd.ExecuteScalarAsync(ct))!;
        await SaveLocationsAsync(conn, newId, d.LocationIDs, ct);
        return newId;
    }

    public async Task UpdateAsync(Department d, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[Departments]
            SET [Code]=@Code,[Name]=@Name,[Description]=@Desc,[IsActive]=@Active
            WHERE [DepartmentID]=@ID;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", d.DepartmentID);
        BindCore(cmd, d);
        await cmd.ExecuteNonQueryAsync(ct);
        await SaveLocationsAsync(conn, d.DepartmentID, d.LocationIDs, ct);
    }

    private static async Task SaveLocationsAsync(SqlConnection conn, int deptId, List<int> locationIds, CancellationToken ct)
    {
        await using var del = new SqlCommand("DELETE FROM [dbo].[LocationDepartments] WHERE [DepartmentID]=@ID;", conn);
        del.Parameters.AddWithValue("@ID", deptId);
        await del.ExecuteNonQueryAsync(ct);
        foreach (var lid in locationIds)
        {
            await using var ins = new SqlCommand(
                "INSERT INTO [dbo].[LocationDepartments]([LocationID],[DepartmentID]) VALUES(@L,@D);", conn);
            ins.Parameters.AddWithValue("@L", lid);
            ins.Parameters.AddWithValue("@D", deptId);
            await ins.ExecuteNonQueryAsync(ct);
        }
    }

    // ── Import ────────────────────────────────────────────────────────────────

    public async Task<ImportResult> ImportAsync(Stream stream, string fileName, char delimiter = ',', CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".xlsx" or ".xls" ? await ImportExcelAsync(ms, ct) : await ImportTextAsync(ms, delimiter, ct);
    }

    private async Task<ImportResult> ImportExcelAsync(Stream stream, CancellationToken ct)
    {
        var result = new ImportResult();
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (int c = 1; c <= lastCol; c++) { var h = ws.Cell(1,c).GetString().Trim(); if (!string.IsNullOrEmpty(h)) headers[h] = c; }
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        var groups = new Dictionary<string, (string Name, string Desc, bool Active, List<string> LocCodes)>(StringComparer.OrdinalIgnoreCase);
        for (int row = 2; row <= lastRow; row++)
        {
            string Get(string key) => headers.TryGetValue(key, out var col) ? ws.Cell(row,col).GetString().Trim() : string.Empty;
            var code = Get("Code");
            if (string.IsNullOrWhiteSpace(code)) { result.RowsSkipped++; continue; }
            var locCode = Get("LocationCode");
            if (!groups.TryGetValue(code, out var g))
            {
                bool active = Get("IsActive") is "" or "1" or "true" or "yes" or "True" or "Yes";
                g = (Get("Name"), Get("Description"), active, []);
                groups[code] = g;
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
        var headers = ParseLine(headerLine, delimiter)
            .Select((h,i) => (h.Trim(),i)).Where(x => !string.IsNullOrEmpty(x.Item1))
            .ToDictionary(x => x.Item1, x => x.i, StringComparer.OrdinalIgnoreCase);
        var groups = new Dictionary<string, (string Name, string Desc, bool Active, List<string> LocCodes)>(StringComparer.OrdinalIgnoreCase);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ParseLine(line, delimiter);
            string Get(string key) => headers.TryGetValue(key, out var idx) && idx < cols.Length ? cols[idx].Trim() : string.Empty;
            var code = Get("Code");
            if (string.IsNullOrWhiteSpace(code)) { result.RowsSkipped++; continue; }
            var locCode = Get("LocationCode");
            if (!groups.TryGetValue(code, out var g))
            {
                bool active = Get("IsActive") is "" or "1" or "true" or "yes" or "True" or "Yes";
                g = (Get("Name"), Get("Description"), active, []);
                groups[code] = g;
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
        foreach (var (code, g) in groups)
        {
            if (string.IsNullOrWhiteSpace(g.Name)) { result.Errors.Add($"Code '{code}': Name is required."); result.RowsSkipped++; continue; }
            try
            {
                await using var conn = new SqlConnection(_cs);
                await conn.OpenAsync(ct);
                var locIds = new List<int>();
                foreach (var lc in g.LocCodes.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    await using var lCmd = new SqlCommand("SELECT [LocationID] FROM [dbo].[Locations] WHERE [Code]=@C;", conn);
                    lCmd.Parameters.AddWithValue("@C", lc.ToUpperInvariant());
                    var lid = await lCmd.ExecuteScalarAsync(ct);
                    if (lid is int l) locIds.Add(l);
                    else result.Errors.Add($"Code '{code}': location '{lc}' not found — skipped.");
                }
                await using var chk = new SqlCommand("SELECT [DepartmentID] FROM [dbo].[Departments] WHERE [Code]=@C;", conn);
                chk.Parameters.AddWithValue("@C", code.ToUpperInvariant());
                var existing = await chk.ExecuteScalarAsync(ct);
                int deptId;
                if (existing is int eid)
                {
                    await using var upd = new SqlCommand(
                        "UPDATE [dbo].[Departments] SET [Code]=@C,[Name]=@N,[Description]=@D,[IsActive]=@A WHERE [DepartmentID]=@ID;", conn);
                    upd.Parameters.AddWithValue("@ID", eid); upd.Parameters.AddWithValue("@C", code.ToUpperInvariant());
                    upd.Parameters.AddWithValue("@N", g.Name); upd.Parameters.AddWithValue("@D", string.IsNullOrWhiteSpace(g.Desc) ? DBNull.Value : g.Desc);
                    upd.Parameters.AddWithValue("@A", g.Active);
                    await upd.ExecuteNonQueryAsync(ct); deptId = eid; result.AccountsUpdated++;
                }
                else
                {
                    await using var ins = new SqlCommand(
                        "INSERT INTO [dbo].[Departments]([Code],[Name],[Description],[IsActive]) OUTPUT INSERTED.[DepartmentID] VALUES(@C,@N,@D,@A);", conn);
                    ins.Parameters.AddWithValue("@C", code.ToUpperInvariant()); ins.Parameters.AddWithValue("@N", g.Name);
                    ins.Parameters.AddWithValue("@D", string.IsNullOrWhiteSpace(g.Desc) ? DBNull.Value : g.Desc); ins.Parameters.AddWithValue("@A", g.Active);
                    deptId = (int)(await ins.ExecuteScalarAsync(ct))!; result.AccountsCreated++;
                }
                await SaveLocationsAsync(conn, deptId, locIds, ct);
            }
            catch (Exception ex) { result.Errors.Add($"Code '{code}': {ex.Message}"); result.RowsSkipped++; }
        }
    }

    private static void BindCore(SqlCommand cmd, Department d)
    {
        cmd.Parameters.AddWithValue("@Code",   d.Code.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@Name",   d.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc",   string.IsNullOrWhiteSpace(d.Description) ? DBNull.Value : d.Description);
        cmd.Parameters.AddWithValue("@Active", d.IsActive);
    }

    private static string[] ParseLine(string line, char delimiter)
    {
        var fields = new List<string>(); var sb = new System.Text.StringBuilder(); bool inQ = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"') { if (inQ && i+1 < line.Length && line[i+1] == '"') { sb.Append('"'); i++; } else inQ = !inQ; }
            else if (c == delimiter && !inQ) { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        fields.Add(sb.ToString()); return [.. fields];
    }

    private static Department Map(SqlDataReader r)
    {
        var locStr = r.GetString(6);
        return new Department
        {
            DepartmentID = r.GetInt32(0), Code = r.GetString(1), Name = r.GetString(2),
            Description  = r.IsDBNull(3) ? string.Empty : r.GetString(3),
            IsActive     = r.GetBoolean(4), CreatedAt = r.GetDateTime(5),
            LocationIDs  = string.IsNullOrEmpty(locStr) ? [] : locStr.Split(',').Select(int.Parse).ToList(),
        };
    }
}
