using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class PayableTermService
{
    private readonly string _cs;
    public PayableTermService(string connectionString) => _cs = connectionString;

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<List<PayableTerm>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT [PayableTermID],[Name],[Description],[ExternalCode],
                   [NumberOfDays],[DateBasis],[CreatedAt],[UpdatedAt]
            FROM   [dbo].[PayableTerms]
            ORDER  BY [Name];
            """;
        var list = new List<PayableTerm>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        await using var r   = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(Map(r));
        return list;
    }

    public async Task<int> CreateAsync(PayableTerm term, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [dbo].[PayableTerms]([Name],[Description],[ExternalCode],[NumberOfDays],[DateBasis])
            OUTPUT INSERTED.[PayableTermID]
            VALUES(@Name,@Desc,@ExtCode,@Days,@Basis);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        BindParams(cmd, term);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateAsync(PayableTerm term, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[PayableTerms]
            SET [Name]=@Name,[Description]=@Desc,[ExternalCode]=@ExtCode,
                [NumberOfDays]=@Days,[DateBasis]=@Basis,[UpdatedAt]=GETDATE()
            WHERE [PayableTermID]=@TermID;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TermID", term.PayableTermID);
        BindParams(cmd, term);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "DELETE FROM [dbo].[PayableTerms] WHERE [PayableTermID]=@ID;", conn);
        cmd.Parameters.AddWithValue("@ID", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Import ────────────────────────────────────────────────────────────────

    public async Task<ImportResult> ImportAsync(Stream stream, string fileName,
        char delimiter = ',', CancellationToken ct = default)
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

    private async Task<ImportResult> ProcessRowsAsync(
        IAsyncEnumerable<Dictionary<string, string>> rows, CancellationToken ct)
    {
        var result = new ImportResult();
        int rowNum = 1;

        await foreach (var row in rows.WithCancellation(ct))
        {
            rowNum++;
            string G(string k) => row.TryGetValue(k, out var v) ? v.Trim() : string.Empty;

            var name = G("Name");
            if (string.IsNullOrWhiteSpace(name)) { result.RowsSkipped++; continue; }

            int? days = null;
            var daysStr = G("NumberOfDays");
            if (!string.IsNullOrWhiteSpace(daysStr) && int.TryParse(daysStr, out var d))
                days = d;

            var basis = G("DateBasis");
            if (basis is not ("Invoice" or "Current" or "FixedDOM"))
                basis = "Invoice";

            try
            {
                var term = new PayableTerm
                {
                    Name         = name,
                    Description  = G("Description"),
                    ExternalCode = G("ExternalCode"),
                    NumberOfDays = days,
                    DateBasis    = basis,
                };
                await UpsertAsync(term, result, ct);
            }
            catch (Exception ex) { result.Errors.Add($"Row {rowNum}: {ex.Message}"); result.RowsSkipped++; }
        }
        return result;
    }

    private async Task UpsertAsync(PayableTerm term, ImportResult result, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        await using var chk = new SqlCommand(
            "SELECT [PayableTermID] FROM [dbo].[PayableTerms] WHERE [Name]=@Name;", conn);
        chk.Parameters.AddWithValue("@Name", term.Name);
        var existing = await chk.ExecuteScalarAsync(ct);

        if (existing is int id)
        {
            const string upd = """
                UPDATE [dbo].[PayableTerms]
                SET [Description]=@Desc,[ExternalCode]=@ExtCode,
                    [NumberOfDays]=@Days,[DateBasis]=@Basis,[UpdatedAt]=GETDATE()
                WHERE [PayableTermID]=@TermID;
                """;
            await using var cmd = new SqlCommand(upd, conn);
            cmd.Parameters.AddWithValue("@TermID", id);
            cmd.Parameters.AddWithValue("@Desc",    (object?)ImportHelper.NullIfEmpty(term.Description) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ExtCode", (object?)ImportHelper.NullIfEmpty(term.ExternalCode) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Days",    (object?)term.NumberOfDays ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Basis",   term.DateBasis);
            await cmd.ExecuteNonQueryAsync(ct);
            result.AccountsUpdated++;
        }
        else
        {
            const string ins = """
                INSERT INTO [dbo].[PayableTerms]([Name],[Description],[ExternalCode],[NumberOfDays],[DateBasis])
                VALUES(@Name,@Desc,@ExtCode,@Days,@Basis);
                """;
            await using var cmd = new SqlCommand(ins, conn);
            BindParams(cmd, term);
            await cmd.ExecuteNonQueryAsync(ct);
            result.AccountsCreated++;
        }
    }

    private static void BindParams(SqlCommand cmd, PayableTerm t)
    {
        cmd.Parameters.AddWithValue("@Name",    t.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc",    (object?)ImportHelper.NullIfEmpty(t.Description) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ExtCode", (object?)ImportHelper.NullIfEmpty(t.ExternalCode) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Days",    (object?)t.NumberOfDays ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Basis",   t.DateBasis);
    }

    private static PayableTerm Map(SqlDataReader r) => new()
    {
        PayableTermID = r.GetInt32(0),
        Name          = r.GetString(1),
        Description   = r.IsDBNull(2) ? string.Empty : r.GetString(2),
        ExternalCode  = r.IsDBNull(3) ? string.Empty : r.GetString(3),
        NumberOfDays  = r.IsDBNull(4) ? null : r.GetInt32(4),
        DateBasis     = r.GetString(5),
        CreatedAt     = r.GetDateTime(6),
        UpdatedAt     = r.GetDateTime(7),
    };
}
