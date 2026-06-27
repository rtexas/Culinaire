using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class JobRoleService
{
    private readonly string _cs;
    public JobRoleService(string connectionString) => _cs = connectionString;

    public async Task<List<JobRole>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT [JobRoleID],[ExternalID],[Name],[Description],[IsExempt],[IsActive],[CreatedAt]
            FROM   [dbo].[JobRoles]
            ORDER  BY [Name];
            """;
        var list = new List<JobRole>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(Map(r));
        return list;
    }

    public async Task<int> CreateAsync(JobRole role, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [dbo].[JobRoles]([ExternalID],[Name],[Description],[IsExempt],[IsActive])
            OUTPUT INSERTED.[JobRoleID]
            VALUES(@ExtID,@Name,@Desc,@Exempt,@Active);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        Bind(cmd, role);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateAsync(JobRole role, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[JobRoles]
            SET [ExternalID]=@ExtID,[Name]=@Name,[Description]=@Desc,[IsExempt]=@Exempt,[IsActive]=@Active
            WHERE [JobRoleID]=@ID;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", role.JobRoleID);
        Bind(cmd, role);
        await cmd.ExecuteNonQueryAsync(ct);
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
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (int row = 2; row <= lastRow; row++)
        {
            string Get(string key) => headers.TryGetValue(key, out var col) ? ws.Cell(row, col).GetString().Trim() : string.Empty;
            try { await UpsertRowAsync(Get("ExternalID"), Get("Name"), Get("Description"), Get("PayType"), Get("IsActive"), result, ct); }
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
        var headers = ImportHelper.ParseLine(headerLine, delimiter)
            .Select((h, i) => (h.Trim(), i))
            .Where(x => !string.IsNullOrEmpty(x.Item1))
            .ToDictionary(x => x.Item1, x => x.i, StringComparer.OrdinalIgnoreCase);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ImportHelper.ParseLine(line, delimiter);
            string Get(string key) => headers.TryGetValue(key, out var idx) && idx < cols.Length ? cols[idx].Trim() : string.Empty;
            try { await UpsertRowAsync(Get("ExternalID"), Get("Name"), Get("Description"), Get("PayType"), Get("IsActive"), result, ct); }
            catch (Exception ex) { result.Errors.Add($"Line: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task UpsertRowAsync(string extId, string name, string desc, string payType, string isActiveStr, ImportResult result, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) { result.RowsSkipped++; return; }
        bool isExempt = payType.Equals("Exempt", StringComparison.OrdinalIgnoreCase)
                     || payType.Equals("Salaried", StringComparison.OrdinalIgnoreCase);
        bool isActive = isActiveStr is "" or "1" or "true" or "yes" or "True" or "Yes";

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        // Find existing by ExternalID (preferred) or Name
        object? existing = null;
        if (!string.IsNullOrWhiteSpace(extId))
        {
            await using var c1 = new SqlCommand("SELECT [JobRoleID] FROM [dbo].[JobRoles] WHERE [ExternalID]=@E;", conn);
            c1.Parameters.AddWithValue("@E", extId);
            existing = await c1.ExecuteScalarAsync(ct);
        }
        if (existing is null)
        {
            await using var c2 = new SqlCommand("SELECT [JobRoleID] FROM [dbo].[JobRoles] WHERE [Name]=@N;", conn);
            c2.Parameters.AddWithValue("@N", name);
            existing = await c2.ExecuteScalarAsync(ct);
        }

        if (existing is int eid)
        {
            await using var upd = new SqlCommand(
                "UPDATE [dbo].[JobRoles] SET [ExternalID]=@E,[Name]=@N,[Description]=@D,[IsExempt]=@X,[IsActive]=@A WHERE [JobRoleID]=@ID;", conn);
            upd.Parameters.AddWithValue("@ID", eid);
            upd.Parameters.AddWithValue("@E",  string.IsNullOrWhiteSpace(extId) ? DBNull.Value : extId);
            upd.Parameters.AddWithValue("@N",  name);
            upd.Parameters.AddWithValue("@D",  string.IsNullOrWhiteSpace(desc) ? DBNull.Value : desc);
            upd.Parameters.AddWithValue("@X",  isExempt);
            upd.Parameters.AddWithValue("@A",  isActive);
            await upd.ExecuteNonQueryAsync(ct);
            result.AccountsUpdated++;
        }
        else
        {
            await using var ins = new SqlCommand(
                "INSERT INTO [dbo].[JobRoles]([ExternalID],[Name],[Description],[IsExempt],[IsActive]) VALUES(@E,@N,@D,@X,@A);", conn);
            ins.Parameters.AddWithValue("@E", string.IsNullOrWhiteSpace(extId) ? DBNull.Value : extId);
            ins.Parameters.AddWithValue("@N", name);
            ins.Parameters.AddWithValue("@D", string.IsNullOrWhiteSpace(desc) ? DBNull.Value : desc);
            ins.Parameters.AddWithValue("@X", isExempt);
            ins.Parameters.AddWithValue("@A", isActive);
            await ins.ExecuteNonQueryAsync(ct);
            result.AccountsCreated++;
        }
    }

    private static void Bind(SqlCommand cmd, JobRole r)
    {
        cmd.Parameters.AddWithValue("@ExtID",  string.IsNullOrWhiteSpace(r.ExternalID) ? DBNull.Value : r.ExternalID);
        cmd.Parameters.AddWithValue("@Name",   r.Name);
        cmd.Parameters.AddWithValue("@Desc",   string.IsNullOrWhiteSpace(r.Description) ? DBNull.Value : r.Description);
        cmd.Parameters.AddWithValue("@Exempt", r.IsExempt);
        cmd.Parameters.AddWithValue("@Active", r.IsActive);
    }

    private static JobRole Map(SqlDataReader r) => new()
    {
        JobRoleID   = r.GetInt32(0),
        ExternalID  = r.IsDBNull(1) ? string.Empty : r.GetString(1),
        Name        = r.GetString(2),
        Description = r.IsDBNull(3) ? string.Empty : r.GetString(3),
        IsExempt    = r.GetBoolean(4),
        IsActive    = r.GetBoolean(5),
        CreatedAt   = r.GetDateTime(6),
    };
}
