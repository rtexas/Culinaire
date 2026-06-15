using Microsoft.Data.SqlClient;

namespace Portal.Services;

public sealed class DashboardService
{
    private readonly string _cs;
    public DashboardService(string connectionString) => _cs = connectionString;

    public record DailyTotal(DateOnly Date, decimal Total);

    public async Task<List<DailyTotal>> GetEodSalesWeekAsync(int locationId, CancellationToken ct = default)
    {
        var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-6));
        const string sql = """
            SELECT e.[EntryDate], SUM(v.[Amount] * sec.[Multiplier])
            FROM   [dbo].[EodSalesEntries] e
            JOIN   [dbo].[EodSalesValues]  v   ON v.[EntryID]     = e.[EntryID]
            JOIN   [dbo].[EodRows]         r   ON r.[RowID]       = v.[RowID]
            JOIN   [dbo].[EodSections]     sec ON sec.[SectionID] = r.[SectionID]
            WHERE  e.[LocationID]    = @LocID
              AND  e.[IsSubmitted]   = 1
              AND  e.[IsVoided]      = 0
              AND  sec.[UseInEodGraph] = 1
              AND  e.[EntryDate]    >= @From
            GROUP  BY e.[EntryDate]
            ORDER  BY e.[EntryDate];
            """;
        return await QueryAsync(sql, locationId, from, ct);
    }

    public async Task<List<DailyTotal>> GetPayablesWeekAsync(int locationId, CancellationToken ct = default)
    {
        var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-6));
        const string sql = """
            SELECT h.[InvoiceDate],
                   SUM(ISNULL(li.[LineTotal], 0) + ISNULL(h.[ShippingCharge], 0) + ISNULL(h.[TaxAmount], 0))
            FROM   [dbo].[PayableHeaders] h
            LEFT JOIN (
                SELECT [PayableID], SUM([ExtendedPrice]) AS [LineTotal]
                FROM   [dbo].[PayableLineItems]
                GROUP  BY [PayableID]
            ) li ON li.[PayableID] = h.[PayableID]
            WHERE  h.[LocationID]  = @LocID
              AND  h.[Status]      = 'Submitted'
              AND  h.[InvoiceDate] >= @From
            GROUP  BY h.[InvoiceDate]
            ORDER  BY h.[InvoiceDate];
            """;
        return await QueryAsync(sql, locationId, from, ct);
    }

    public async Task<List<DailyTotal>> GetChecksWeekAsync(int locationId, CancellationToken ct = default)
    {
        var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-6));
        const string sql = """
            SELECT [TransactionDate], SUM([Amount])
            FROM   [dbo].[CheckTransactions]
            WHERE  [LocationID]      = @LocID
              AND  [IsSubmitted]     = 1
              AND  [IsVoided]        = 0
              AND  [TransactionDate] >= @From
            GROUP  BY [TransactionDate]
            ORDER  BY [TransactionDate];
            """;
        return await QueryAsync(sql, locationId, from, ct);
    }

    public async Task<List<DailyTotal>> GetPayrollWeekAsync(int locationId, CancellationToken ct = default)
    {
        var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-6));
        const string sql = """
            SELECT [PayPeriodEnd], SUM([GrandTotal])
            FROM   [dbo].[PayrollRuns]
            WHERE  [LocationID]   = @LocID
              AND  [Status]       = 'Submitted'
              AND  [PayPeriodEnd] >= @From
            GROUP  BY [PayPeriodEnd]
            ORDER  BY [PayPeriodEnd];
            """;
        return await QueryAsync(sql, locationId, from, ct);
    }

    private async Task<List<DailyTotal>> QueryAsync(string sql, int locationId, DateOnly from, CancellationToken ct)
    {
        var list = new List<DailyTotal>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@LocID", locationId);
        cmd.Parameters.AddWithValue("@From",  from.ToDateTime(TimeOnly.MinValue));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new DailyTotal(DateOnly.FromDateTime(r.GetDateTime(0)), r.GetDecimal(1)));
        return list;
    }
}
