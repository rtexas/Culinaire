using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class PayrollRunService
{
    private readonly string _cs;

    public PayrollRunService(string connectionString) => _cs = connectionString;

    // ── Pay-period computation ────────────────────────────────────────────────

    public async Task<(DateOnly Start, DateOnly End)> ComputeNextPeriodAsync(
        int batchId, string payPeriodLength, string? startDayOfWeek, CancellationToken ct = default)
    {
        var lastEnd = await GetLastPeriodEndAsync(batchId, ct);
        return ComputePeriod(payPeriodLength, startDayOfWeek, lastEnd);
    }

    private async Task<DateOnly?> GetLastPeriodEndAsync(int batchId, CancellationToken ct)
    {
        const string sql = """
            SELECT TOP 1 [PayPeriodEnd]
            FROM   [dbo].[PayrollRuns]
            WHERE  [BatchID]=@BID AND [Status] <> 'Voided'
            ORDER  BY [PayPeriodEnd] DESC;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@BID", batchId);
        var val = await cmd.ExecuteScalarAsync(ct);
        return val is DateTime dt ? DateOnly.FromDateTime(dt) : null;
    }

    public static (DateOnly Start, DateOnly End) ComputePeriod(
        string payPeriodLength, string? startDayOfWeek, DateOnly? lastEnd)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        if (payPeriodLength == "Monthly")
        {
            DateOnly start = lastEnd is null
                ? new DateOnly(today.Year, today.Month, 1)
                : new DateOnly(lastEnd.Value.Year, lastEnd.Value.Month, 1).AddMonths(1);
            return (start, start.AddMonths(1).AddDays(-1));
        }

        if (payPeriodLength == "1st and 15th")
        {
            DateOnly start;
            if (lastEnd is null)
                start = today.Day < 15
                    ? new DateOnly(today.Year, today.Month, 1)
                    : new DateOnly(today.Year, today.Month, 15);
            else
                start = lastEnd.Value.Day < 15
                    ? new DateOnly(lastEnd.Value.Year, lastEnd.Value.Month, 15)
                    : new DateOnly(lastEnd.Value.Year, lastEnd.Value.Month, 1).AddMonths(1);
            DateOnly end = start.Day == 1
                ? new DateOnly(start.Year, start.Month, 14)
                : new DateOnly(start.Year, start.Month, 1).AddMonths(1).AddDays(-1);
            return (start, end);
        }

        int days = payPeriodLength switch
        {
            "Every Two Weeks" => 14,
            "Weekly"          => 7,
            _                 => 1   // Daily
        };

        DateOnly periodStart;
        if (lastEnd is null)
        {
            if (startDayOfWeek is not null
                && Enum.TryParse<DayOfWeek>(startDayOfWeek, out var dow))
            {
                int diff = ((int)today.DayOfWeek - (int)dow + 7) % 7;
                periodStart = today.AddDays(-diff);
            }
            else
                periodStart = today;
        }
        else
            periodStart = lastEnd.Value.AddDays(1);

        return (periodStart, periodStart.AddDays(days - 1));
    }

    public static string ResolveBatchName(string template, string locationName,
        DateOnly start, DateOnly end) =>
        template
            .Replace("{Location}",      locationName)
            .Replace("{Department}",    string.Empty)
            .Replace("{FromPayPeriod}", start.ToString("MM/dd/yyyy"))
            .Replace("{ToPayPeriod}",   end.ToString("MM/dd/yyyy"));

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<List<PayrollRun>> GetRecentAsync(int locationId, int top = 20, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP(@Top)
                   r.[RunID],r.[BatchID],r.[LocationID],ISNULL(l.[Name],''),
                   r.[BatchName],r.[PayPeriodStart],r.[PayPeriodEnd],
                   r.[Status],r.[GrandTotal],r.[CreatedAt],r.[SubmittedAt],r.[VoidedAt]
            FROM   [dbo].[PayrollRuns] r
            JOIN   [dbo].[Locations]   l ON l.[LocationID]=r.[LocationID]
            WHERE  r.[LocationID]=@LocID
            ORDER  BY r.[PayPeriodStart] DESC, r.[RunID] DESC;
            """;
        var list = new List<PayrollRun>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@LocID", locationId);
        cmd.Parameters.AddWithValue("@Top",   top);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(MapHeader(r));
        return list;
    }

    public async Task<PayrollRun?> GetByIdAsync(int runId, CancellationToken ct = default)
    {
        const string headerSql = """
            SELECT r.[RunID],r.[BatchID],r.[LocationID],ISNULL(l.[Name],''),
                   r.[BatchName],r.[PayPeriodStart],r.[PayPeriodEnd],
                   r.[Status],r.[GrandTotal],r.[CreatedAt],r.[SubmittedAt],r.[VoidedAt]
            FROM   [dbo].[PayrollRuns] r
            JOIN   [dbo].[Locations]   l ON l.[LocationID]=r.[LocationID]
            WHERE  r.[RunID]=@ID;
            """;
        const string linesSql = """
            SELECT ln.[LineID],ln.[RunID],ln.[EmployeeID],ISNULL(e.[Name],''),
                   ln.[JobRoleID],ISNULL(j.[Name],''),
                   ln.[PayType],ln.[Quantity],ln.[PayRate],ln.[TotalAmount],ln.[SortOrder]
            FROM   [dbo].[PayrollRunLines] ln
            JOIN   [dbo].[Employees]       e ON e.[EmployeeID]=ln.[EmployeeID]
            JOIN   [dbo].[JobRoles]        j ON j.[JobRoleID] =ln.[JobRoleID]
            WHERE  ln.[RunID]=@ID
            ORDER  BY ln.[SortOrder], ln.[LineID];
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        PayrollRun? run = null;
        await using (var cmd = new SqlCommand(headerSql, conn))
        {
            cmd.Parameters.AddWithValue("@ID", runId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct)) run = MapHeader(r);
        }
        if (run is null) return null;

        await using (var cmd = new SqlCommand(linesSql, conn))
        {
            cmd.Parameters.AddWithValue("@ID", runId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                run.Lines.Add(MapLine(r));
        }
        return run;
    }

    public async Task<int> CreateAsync(PayrollRun run, CancellationToken ct = default)
    {
        const string insertRun = """
            INSERT INTO [dbo].[PayrollRuns]
                ([BatchID],[LocationID],[BatchName],[PayPeriodStart],[PayPeriodEnd],[Status],[GrandTotal])
            OUTPUT INSERTED.[RunID]
            VALUES(@BID,@LocID,@Name,@Start,@End,'Saved',@Total);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var tx = conn.BeginTransaction();

        int runId;
        await using (var cmd = new SqlCommand(insertRun, conn, tx))
        {
            BindHeader(cmd, run);
            runId = (int)(await cmd.ExecuteScalarAsync(ct))!;
        }
        await SaveLinesAsync(conn, tx, runId, run.Lines, ct);
        await tx.CommitAsync(ct);
        return runId;
    }

    public async Task UpdateAsync(PayrollRun run, CancellationToken ct = default)
    {
        const string updSql = """
            UPDATE [dbo].[PayrollRuns]
            SET [BatchName]=@Name,[PayPeriodStart]=@Start,[PayPeriodEnd]=@End,[GrandTotal]=@Total
            WHERE [RunID]=@ID AND [Status]='Saved';
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var tx = conn.BeginTransaction();

        await using (var cmd = new SqlCommand(updSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("@ID",    run.RunID);
            cmd.Parameters.AddWithValue("@Name",  run.BatchName);
            cmd.Parameters.AddWithValue("@Start", run.PayPeriodStart.ToDateTime(TimeOnly.MinValue));
            cmd.Parameters.AddWithValue("@End",   run.PayPeriodEnd.ToDateTime(TimeOnly.MinValue));
            cmd.Parameters.AddWithValue("@Total", run.GrandTotal);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var del = new SqlCommand(
            "DELETE FROM [dbo].[PayrollRunLines] WHERE [RunID]=@ID;", conn, tx))
        {
            del.Parameters.AddWithValue("@ID", run.RunID);
            await del.ExecuteNonQueryAsync(ct);
        }
        await SaveLinesAsync(conn, tx, run.RunID, run.Lines, ct);
        await tx.CommitAsync(ct);
    }

    public async Task SubmitAsync(int runId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[PayrollRuns]
            SET [Status]='Submitted',[SubmittedAt]=GETDATE()
            WHERE [RunID]=@ID AND [Status]='Saved';
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", runId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task VoidAsync(int runId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[PayrollRuns]
            SET [Status]='Voided',[VoidedAt]=GETDATE()
            WHERE [RunID]=@ID AND [Status]='Saved';
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", runId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static async Task SaveLinesAsync(SqlConnection conn, SqlTransaction tx,
        int runId, List<PayrollRunLine> lines, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO [dbo].[PayrollRunLines]
                ([RunID],[EmployeeID],[JobRoleID],[PayType],[Quantity],[PayRate],[TotalAmount],[SortOrder])
            VALUES(@RunID,@EmpID,@RoleID,@PayType,@Qty,@Rate,@Total,@Sort);
            """;
        for (int i = 0; i < lines.Count; i++)
        {
            var ln = lines[i];
            await using var cmd = new SqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@RunID",   runId);
            cmd.Parameters.AddWithValue("@EmpID",   ln.EmployeeID);
            cmd.Parameters.AddWithValue("@RoleID",  ln.JobRoleID);
            cmd.Parameters.AddWithValue("@PayType", ln.PayType);
            cmd.Parameters.AddWithValue("@Qty",     ln.Quantity);
            cmd.Parameters.AddWithValue("@Rate",    ln.PayRate);
            cmd.Parameters.AddWithValue("@Total",   ln.TotalAmount);
            cmd.Parameters.AddWithValue("@Sort",    i);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static void BindHeader(SqlCommand cmd, PayrollRun r)
    {
        cmd.Parameters.AddWithValue("@BID",   r.BatchID);
        cmd.Parameters.AddWithValue("@LocID", r.LocationID);
        cmd.Parameters.AddWithValue("@Name",  r.BatchName);
        cmd.Parameters.AddWithValue("@Start", r.PayPeriodStart.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@End",   r.PayPeriodEnd.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@Total", r.GrandTotal);
    }

    private static PayrollRun MapHeader(SqlDataReader r) => new()
    {
        RunID          = r.GetInt32(0),
        BatchID        = r.GetInt32(1),
        LocationID     = r.GetInt32(2),
        LocationName   = r.GetString(3),
        BatchName      = r.GetString(4),
        PayPeriodStart = DateOnly.FromDateTime(r.GetDateTime(5)),
        PayPeriodEnd   = DateOnly.FromDateTime(r.GetDateTime(6)),
        Status         = r.GetString(7),
        GrandTotal     = r.GetDecimal(8),
        CreatedAt      = r.GetDateTime(9),
        SubmittedAt    = r.IsDBNull(10) ? null : r.GetDateTime(10),
        VoidedAt       = r.IsDBNull(11) ? null : r.GetDateTime(11),
    };

    private static PayrollRunLine MapLine(SqlDataReader r) => new()
    {
        LineID       = r.GetInt32(0),
        RunID        = r.GetInt32(1),
        EmployeeID   = r.GetInt32(2),
        EmployeeName = r.GetString(3),
        JobRoleID    = r.GetInt32(4),
        JobRoleName  = r.GetString(5),
        PayType      = r.GetString(6),
        Quantity     = r.GetDecimal(7),
        PayRate      = r.GetDecimal(8),
        TotalAmount  = r.GetDecimal(9),
        SortOrder    = r.GetInt32(10),
    };
}
