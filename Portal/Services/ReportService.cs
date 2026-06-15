using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class ReportService
{
    private readonly string _cs;
    public ReportService(string connectionString) => _cs = connectionString;

    // ── Report Catalog ──────────────────────────────────────────────────────────
    public static readonly IReadOnlyList<ReportDef> All =
    [
        // EOD Sales
        new("eod-daily",          "Daily Sales Total",          "EOD Sales", "Submitted EOD Sales total per day."),
        new("eod-by-section",     "Sales by Section",           "EOD Sales", "Total sales broken out by EOD section for the period."),
        new("eod-section-daily",  "Sales by Section by Date",   "EOD Sales", "Section totals for each submitted date."),
        new("eod-monthly",        "Monthly Sales Summary",      "EOD Sales", "EOD Sales totals grouped by calendar month."),
        new("eod-unit-accounts",  "Unit Account Totals",        "EOD Sales", "Totals for non-sales tracking sections (unit accounts, liabilities)."),
        new("eod-submission-log", "Submission Log",             "EOD Sales", "History of all EOD entry submissions and voids."),

        // Payables
        new("payables-register",      "Payables Register",          "Payables",  "All submitted invoices with vendor, date, and total."),
        new("payables-by-vendor",     "Vendor Spend Summary",       "Payables",  "Invoice count and total spend per vendor."),
        new("payables-monthly",       "Monthly AP Summary",         "Payables",  "Submitted payables totals grouped by calendar month."),
        new("payables-open",          "Open (Unsent) Invoices",     "Payables",  "Saved invoices that have not yet been submitted.", HasDateFilter: false),
        new("payables-submission-log","Submission Log",             "Payables",  "History of all invoice status changes (submitted, voided)."),

        // Checks
        new("check-register",         "Check Register",             "Checks",    "All submitted, non-voided checks in date range."),
        new("checks-by-payee",        "Checks by Payee",            "Checks",    "Total disbursements per payee for the period."),
        new("checks-voided",          "Voided Checks",              "Checks",    "All voided checks with void date."),
        new("checks-monthly",         "Monthly Disbursements",      "Checks",    "Submitted check totals grouped by calendar month."),
        new("checks-submission-log",  "Submission Log",             "Checks",    "History of all check submissions and voids."),

        // Payroll
        new("payroll-by-period",      "Payroll by Pay Period",      "Payroll",   "Each submitted payroll run with pay period and grand total."),
        new("payroll-by-employee",    "Employee Earnings",          "Payroll",   "Total earnings per employee for the period."),
        new("payroll-by-role",        "Labor by Job Role",          "Payroll",   "Total hours and pay grouped by job role."),
        new("payroll-ytd",            "Year-to-Date Payroll",       "Payroll",   "Cumulative submitted payroll from Jan 1 of the current year.", HasDateFilter: false),
        new("payroll-monthly",        "Monthly Payroll Summary",    "Payroll",   "Submitted payroll totals grouped by calendar month."),
        new("payroll-run-detail",     "Payroll Run Detail",         "Payroll",   "Line-level detail for all submitted payroll runs in the period."),
        new("payroll-submission-log", "Submission Log",             "Payroll",   "History of all payroll run status changes (submitted, voided)."),

        // Security
        new("security-user-locations",  "User Location Access",    "Security", "Each user and the locations they are permitted to access.", HasDateFilter: false, HasLocationFilter: false),
        new("security-user-modules",    "User Module Permissions",  "Security", "Each user and their permission level for each module.",    HasDateFilter: false, HasLocationFilter: false),
        new("security-user-summary",    "User Access Summary",      "Security", "Combined view: user role, assigned locations, and module permissions.", HasDateFilter: false, HasLocationFilter: false),

        // Financial Overview
        new("financial-overview",  "Location Financial Overview","Financial Overview","EOD Sales, Payables, Checks, and Payroll totals for the period."),
        new("financial-cashflow",  "Cash Flow Summary",          "Financial Overview","Daily inflows (EOD Sales) vs outflows (Checks + Payables)."),
        new("financial-monthly",   "Monthly Module Comparison",  "Financial Overview","Side-by-side monthly totals across all four modules."),
        new("financial-annual",    "Annual Summary",             "Financial Overview","Year-by-year totals across all modules.", HasDateFilter: false),
    ];

    public static ReportDef? Find(string id) =>
        All.FirstOrDefault(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    // ── Dispatcher ─────────────────────────────────────────────────────────────
    public Task<ReportResult> RunAsync(string id, int locationId, DateOnly from, DateOnly to,
                                       CancellationToken ct = default)
        => id switch
        {
            "eod-daily"           => EodDailyAsync(locationId, from, to, ct),
            "eod-by-section"      => EodBySectionAsync(locationId, from, to, ct),
            "eod-section-daily"   => EodSectionDailyAsync(locationId, from, to, ct),
            "eod-submission-log"  => EodSubmissionLogAsync(locationId, from, to, ct),
            "eod-monthly"         => EodMonthlyAsync(locationId, from, to, ct),
            "eod-unit-accounts"   => EodUnitAccountsAsync(locationId, from, to, ct),
            "payables-register"   => PayablesRegisterAsync(locationId, from, to, ct),
            "payables-by-vendor"  => PayablesByVendorAsync(locationId, from, to, ct),
            "payables-monthly"    => PayablesMonthlyAsync(locationId, from, to, ct),
            "payables-open"           => PayablesOpenAsync(locationId, ct),
            "payables-submission-log" => PayablesSubmissionLogAsync(locationId, from, to, ct),
            "check-register"          => CheckRegisterAsync(locationId, from, to, ct),
            "checks-by-payee"     => ChecksByPayeeAsync(locationId, from, to, ct),
            "checks-voided"       => ChecksVoidedAsync(locationId, from, to, ct),
            "checks-monthly"          => ChecksMonthlyAsync(locationId, from, to, ct),
            "checks-submission-log"   => ChecksSubmissionLogAsync(locationId, from, to, ct),
            "payroll-by-period"       => PayrollByPeriodAsync(locationId, from, to, ct),
            "payroll-by-employee" => PayrollByEmployeeAsync(locationId, from, to, ct),
            "payroll-by-role"     => PayrollByRoleAsync(locationId, from, to, ct),
            "payroll-ytd"         => PayrollYtdAsync(locationId, ct),
            "payroll-monthly"     => PayrollMonthlyAsync(locationId, from, to, ct),
            "payroll-run-detail"      => PayrollRunDetailAsync(locationId, from, to, ct),
            "payroll-submission-log"  => PayrollSubmissionLogAsync(locationId, from, to, ct),
            "security-user-locations"  => SecurityUserLocationsAsync(ct),
            "security-user-modules"    => SecurityUserModulesAsync(ct),
            "security-user-summary"    => SecurityUserSummaryAsync(ct),
            "financial-overview"       => FinancialOverviewAsync(locationId, from, to, ct),
            "financial-cashflow"  => FinancialCashFlowAsync(locationId, from, to, ct),
            "financial-monthly"   => FinancialMonthlyAsync(locationId, from, to, ct),
            "financial-annual"    => FinancialAnnualAsync(locationId, ct),
            _                     => Task.FromResult(new ReportResult())
        };

    // ── EOD Sales ──────────────────────────────────────────────────────────────

    private Task<ReportResult> EodDailyAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT CONVERT(VARCHAR(10), e.[EntryDate], 101)    AS [Date],
                   SUM(v.[Amount] * sec.[Multiplier])           AS [EOD Sales Total]
            FROM   [dbo].[EodSalesEntries] e
            JOIN   [dbo].[EodSalesValues]  v   ON v.[EntryID]     = e.[EntryID]
            JOIN   [dbo].[EodRows]         r   ON r.[RowID]       = v.[RowID]
            JOIN   [dbo].[EodSections]     sec ON sec.[SectionID] = r.[SectionID]
            WHERE  e.[LocationID]  = @Loc
              AND  e.[IsSubmitted] = 1
              AND  e.[IsVoided]    = 0
              AND  sec.[UseInEodSales] = 1
              AND  e.[EntryDate] BETWEEN @From AND @To
            GROUP  BY e.[EntryDate]
            ORDER  BY e.[EntryDate];
            """, loc, from, to, ct);

    private Task<ReportResult> EodBySectionAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT sec.[Name]                                   AS [Section],
                   SUM(v.[Amount] * sec.[Multiplier])           AS [Total]
            FROM   [dbo].[EodSalesEntries] e
            JOIN   [dbo].[EodSalesValues]  v   ON v.[EntryID]     = e.[EntryID]
            JOIN   [dbo].[EodRows]         r   ON r.[RowID]       = v.[RowID]
            JOIN   [dbo].[EodSections]     sec ON sec.[SectionID] = r.[SectionID]
            WHERE  e.[LocationID]  = @Loc
              AND  e.[IsSubmitted] = 1
              AND  e.[IsVoided]    = 0
              AND  sec.[UseInEodSales] = 1
              AND  e.[EntryDate] BETWEEN @From AND @To
            GROUP  BY sec.[SectionID], sec.[Name]
            ORDER  BY sec.[Name];
            """, loc, from, to, ct);

    private Task<ReportResult> EodSectionDailyAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT CONVERT(VARCHAR(10), e.[EntryDate], 101)    AS [Date],
                   sec.[Name]                                   AS [Section],
                   SUM(v.[Amount] * sec.[Multiplier])           AS [Total]
            FROM   [dbo].[EodSalesEntries] e
            JOIN   [dbo].[EodSalesValues]  v   ON v.[EntryID]     = e.[EntryID]
            JOIN   [dbo].[EodRows]         r   ON r.[RowID]       = v.[RowID]
            JOIN   [dbo].[EodSections]     sec ON sec.[SectionID] = r.[SectionID]
            WHERE  e.[LocationID]  = @Loc
              AND  e.[IsSubmitted] = 1
              AND  e.[IsVoided]    = 0
              AND  sec.[UseInEodSales] = 1
              AND  e.[EntryDate] BETWEEN @From AND @To
            GROUP  BY e.[EntryDate], sec.[SectionID], sec.[Name]
            ORDER  BY e.[EntryDate], sec.[Name];
            """, loc, from, to, ct);

    private Task<ReportResult> EodSubmissionLogAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT CONVERT(VARCHAR(10), e.[EntryDate], 101)    AS [Entry Date],
                   CASE WHEN e.[IsVoided]    = 1 THEN 'Voided'
                        WHEN e.[IsSubmitted] = 1 THEN 'Submitted'
                        ELSE 'Saved' END                        AS [Status],
                   CONVERT(VARCHAR(19), e.[SubmittedAt], 120)  AS [Submitted At],
                   CONVERT(VARCHAR(19), e.[VoidedAt],    120)  AS [Voided At]
            FROM   [dbo].[EodSalesEntries] e
            WHERE  e.[LocationID] = @Loc
              AND  e.[EntryDate] BETWEEN @From AND @To
            ORDER  BY e.[EntryDate] DESC;
            """, loc, from, to, ct);

    private Task<ReportResult> EodMonthlyAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT CAST(YEAR(e.[EntryDate]) AS VARCHAR(4)) + '-'
                   + RIGHT('0' + CAST(MONTH(e.[EntryDate]) AS VARCHAR(2)), 2) AS [Month],
                   SUM(v.[Amount] * sec.[Multiplier])                          AS [EOD Sales Total]
            FROM   [dbo].[EodSalesEntries] e
            JOIN   [dbo].[EodSalesValues]  v   ON v.[EntryID]     = e.[EntryID]
            JOIN   [dbo].[EodRows]         r   ON r.[RowID]       = v.[RowID]
            JOIN   [dbo].[EodSections]     sec ON sec.[SectionID] = r.[SectionID]
            WHERE  e.[LocationID]  = @Loc
              AND  e.[IsSubmitted] = 1
              AND  e.[IsVoided]    = 0
              AND  sec.[UseInEodSales] = 1
              AND  e.[EntryDate] BETWEEN @From AND @To
            GROUP  BY YEAR(e.[EntryDate]), MONTH(e.[EntryDate])
            ORDER  BY YEAR(e.[EntryDate]), MONTH(e.[EntryDate]);
            """, loc, from, to, ct);

    private Task<ReportResult> EodUnitAccountsAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT sec.[Name]                                   AS [Section],
                   r.[Name]                                     AS [Row],
                   SUM(ABS(v.[Amount]))                         AS [Total]
            FROM   [dbo].[EodSalesEntries] e
            JOIN   [dbo].[EodSalesValues]  v   ON v.[EntryID]     = e.[EntryID]
            JOIN   [dbo].[EodRows]         r   ON r.[RowID]       = v.[RowID]
            JOIN   [dbo].[EodSections]     sec ON sec.[SectionID] = r.[SectionID]
            WHERE  e.[LocationID]    = @Loc
              AND  e.[IsSubmitted]   = 1
              AND  e.[IsVoided]      = 0
              AND  sec.[UseInEodSales] = 0
              AND  e.[EntryDate] BETWEEN @From AND @To
            GROUP  BY sec.[SectionID], sec.[Name], r.[RowID], r.[Name]
            ORDER  BY sec.[Name], r.[Name];
            """, loc, from, to, ct);

    // ── Payables ───────────────────────────────────────────────────────────────

    private Task<ReportResult> PayablesRegisterAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT CONVERT(VARCHAR(10), h.[InvoiceDate], 101)  AS [Invoice Date],
                   h.[InvoiceNumber]                            AS [Invoice #],
                   v.[Name]                                     AS [Vendor],
                   ISNULL(li.[LineTotal], 0)                    AS [Lines Total],
                   ISNULL(h.[ShippingCharge], 0)                AS [Shipping],
                   ISNULL(h.[TaxAmount], 0)                     AS [Tax],
                   ISNULL(li.[LineTotal], 0)
                     + ISNULL(h.[ShippingCharge], 0)
                     + ISNULL(h.[TaxAmount], 0)                 AS [Invoice Total]
            FROM   [dbo].[PayableHeaders] h
            JOIN   [dbo].[Vendors] v ON v.[VendorID] = h.[VendorID]
            LEFT JOIN (
                SELECT [PayableID], SUM([ExtendedPrice]) AS [LineTotal]
                FROM   [dbo].[PayableLineItems]
                GROUP  BY [PayableID]
            ) li ON li.[PayableID] = h.[PayableID]
            WHERE  h.[LocationID]  = @Loc
              AND  h.[Status]      = 'Submitted'
              AND  h.[InvoiceDate] BETWEEN @From AND @To
            ORDER  BY h.[InvoiceDate] DESC, v.[Name];
            """, loc, from, to, ct);

    private Task<ReportResult> PayablesByVendorAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT v.[Name]                                     AS [Vendor],
                   COUNT(*)                                     AS [Invoices],
                   SUM(ISNULL(li.[LineTotal], 0)
                     + ISNULL(h.[ShippingCharge], 0)
                     + ISNULL(h.[TaxAmount], 0))                AS [Total Spend]
            FROM   [dbo].[PayableHeaders] h
            JOIN   [dbo].[Vendors] v ON v.[VendorID] = h.[VendorID]
            LEFT JOIN (
                SELECT [PayableID], SUM([ExtendedPrice]) AS [LineTotal]
                FROM   [dbo].[PayableLineItems]
                GROUP  BY [PayableID]
            ) li ON li.[PayableID] = h.[PayableID]
            WHERE  h.[LocationID]  = @Loc
              AND  h.[Status]      = 'Submitted'
              AND  h.[InvoiceDate] BETWEEN @From AND @To
            GROUP  BY v.[VendorID], v.[Name]
            ORDER  BY [Total Spend] DESC;
            """, loc, from, to, ct);

    private Task<ReportResult> PayablesMonthlyAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT CAST(YEAR(h.[InvoiceDate]) AS VARCHAR(4)) + '-'
                   + RIGHT('0' + CAST(MONTH(h.[InvoiceDate]) AS VARCHAR(2)), 2) AS [Month],
                   COUNT(*)                                     AS [Invoices],
                   SUM(ISNULL(li.[LineTotal], 0)
                     + ISNULL(h.[ShippingCharge], 0)
                     + ISNULL(h.[TaxAmount], 0))                AS [Total]
            FROM   [dbo].[PayableHeaders] h
            LEFT JOIN (
                SELECT [PayableID], SUM([ExtendedPrice]) AS [LineTotal]
                FROM   [dbo].[PayableLineItems]
                GROUP  BY [PayableID]
            ) li ON li.[PayableID] = h.[PayableID]
            WHERE  h.[LocationID]  = @Loc
              AND  h.[Status]      = 'Submitted'
              AND  h.[InvoiceDate] BETWEEN @From AND @To
            GROUP  BY YEAR(h.[InvoiceDate]), MONTH(h.[InvoiceDate])
            ORDER  BY YEAR(h.[InvoiceDate]), MONTH(h.[InvoiceDate]);
            """, loc, from, to, ct);

    private async Task<ReportResult> PayablesOpenAsync(int loc, CancellationToken ct)
    {
        var from = DateOnly.MinValue;
        var to   = DateOnly.MaxValue;
        return await QueryAsync("""
            SELECT CONVERT(VARCHAR(10), h.[InvoiceDate], 101)  AS [Invoice Date],
                   h.[InvoiceNumber]                            AS [Invoice #],
                   v.[Name]                                     AS [Vendor],
                   DATEDIFF(DAY, h.[InvoiceDate], GETDATE())    AS [Days Old],
                   ISNULL(li.[LineTotal], 0)
                     + ISNULL(h.[ShippingCharge], 0)
                     + ISNULL(h.[TaxAmount], 0)                 AS [Invoice Total]
            FROM   [dbo].[PayableHeaders] h
            JOIN   [dbo].[Vendors] v ON v.[VendorID] = h.[VendorID]
            LEFT JOIN (
                SELECT [PayableID], SUM([ExtendedPrice]) AS [LineTotal]
                FROM   [dbo].[PayableLineItems]
                GROUP  BY [PayableID]
            ) li ON li.[PayableID] = h.[PayableID]
            WHERE  h.[LocationID] = @Loc
              AND  h.[Status]     = 'Saved'
            ORDER  BY h.[InvoiceDate] ASC;
            """, loc, from, to, ct);
    }

    private Task<ReportResult> PayablesSubmissionLogAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT CONVERT(VARCHAR(10), h.[InvoiceDate], 101)      AS [Invoice Date],
                   h.[InvoiceNumber]                                AS [Invoice #],
                   v.[Name]                                         AS [Vendor],
                   h.[Status]                                       AS [Status],
                   CONVERT(VARCHAR(19), h.[CreatedAt], 120)        AS [Created At],
                   CONVERT(VARCHAR(19), h.[UpdatedAt], 120)        AS [Last Updated]
            FROM   [dbo].[PayableHeaders] h
            JOIN   [dbo].[Vendors] v ON v.[VendorID] = h.[VendorID]
            WHERE  h.[LocationID]  = @Loc
              AND  h.[InvoiceDate] BETWEEN @From AND @To
            ORDER  BY h.[UpdatedAt] DESC;
            """, loc, from, to, ct);

    // ── Checks ─────────────────────────────────────────────────────────────────

    private Task<ReportResult> CheckRegisterAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT CONVERT(VARCHAR(10), ct.[TransactionDate], 101) AS [Date],
                   ct.[CheckNumber]                                  AS [Check #],
                   CASE WHEN ct.[IsManualVendor] = 1
                        THEN ct.[ManualVendorName]
                        ELSE v.[Name] END                            AS [Payee],
                   ct.[Amount]                                       AS [Amount],
                   ct.[Memo]                                         AS [Memo]
            FROM   [dbo].[CheckTransactions] ct
            LEFT JOIN [dbo].[Vendors] v ON v.[VendorID] = ct.[VendorID]
            WHERE  ct.[LocationID]  = @Loc
              AND  ct.[IsSubmitted] = 1
              AND  ct.[IsVoided]    = 0
              AND  ct.[TransactionDate] BETWEEN @From AND @To
            ORDER  BY ct.[TransactionDate] DESC, ct.[CheckNumber];
            """, loc, from, to, ct);

    private Task<ReportResult> ChecksByPayeeAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT CASE WHEN ct.[IsManualVendor] = 1
                        THEN ct.[ManualVendorName]
                        ELSE v.[Name] END                            AS [Payee],
                   COUNT(*)                                          AS [Checks],
                   SUM(ct.[Amount])                                  AS [Total]
            FROM   [dbo].[CheckTransactions] ct
            LEFT JOIN [dbo].[Vendors] v ON v.[VendorID] = ct.[VendorID]
            WHERE  ct.[LocationID]  = @Loc
              AND  ct.[IsSubmitted] = 1
              AND  ct.[IsVoided]    = 0
              AND  ct.[TransactionDate] BETWEEN @From AND @To
            GROUP  BY CASE WHEN ct.[IsManualVendor] = 1
                           THEN ct.[ManualVendorName]
                           ELSE v.[Name] END
            ORDER  BY [Total] DESC;
            """, loc, from, to, ct);

    private Task<ReportResult> ChecksVoidedAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT CONVERT(VARCHAR(10), ct.[TransactionDate], 101) AS [Check Date],
                   ct.[CheckNumber]                                  AS [Check #],
                   CASE WHEN ct.[IsManualVendor] = 1
                        THEN ct.[ManualVendorName]
                        ELSE v.[Name] END                            AS [Payee],
                   ct.[Amount]                                       AS [Amount],
                   CONVERT(VARCHAR(19), ct.[VoidedAt], 120)         AS [Voided At]
            FROM   [dbo].[CheckTransactions] ct
            LEFT JOIN [dbo].[Vendors] v ON v.[VendorID] = ct.[VendorID]
            WHERE  ct.[LocationID] = @Loc
              AND  ct.[IsVoided]   = 1
              AND  ct.[TransactionDate] BETWEEN @From AND @To
            ORDER  BY ct.[VoidedAt] DESC;
            """, loc, from, to, ct);

    private Task<ReportResult> ChecksMonthlyAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT CAST(YEAR(ct.[TransactionDate]) AS VARCHAR(4)) + '-'
                   + RIGHT('0' + CAST(MONTH(ct.[TransactionDate]) AS VARCHAR(2)), 2) AS [Month],
                   COUNT(*)                                          AS [Checks],
                   SUM(ct.[Amount])                                  AS [Total Disbursed]
            FROM   [dbo].[CheckTransactions] ct
            WHERE  ct.[LocationID]  = @Loc
              AND  ct.[IsSubmitted] = 1
              AND  ct.[IsVoided]    = 0
              AND  ct.[TransactionDate] BETWEEN @From AND @To
            GROUP  BY YEAR(ct.[TransactionDate]), MONTH(ct.[TransactionDate])
            ORDER  BY YEAR(ct.[TransactionDate]), MONTH(ct.[TransactionDate]);
            """, loc, from, to, ct);

    private Task<ReportResult> ChecksSubmissionLogAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT CONVERT(VARCHAR(10), ct.[TransactionDate], 101) AS [Check Date],
                   ct.[CheckNumber]                                  AS [Check #],
                   CASE WHEN ct.[IsManualVendor] = 1
                        THEN ct.[ManualVendorName]
                        ELSE v.[Name] END                            AS [Payee],
                   ct.[Amount]                                       AS [Amount],
                   CASE WHEN ct.[IsVoided]    = 1 THEN 'Voided'
                        WHEN ct.[IsSubmitted] = 1 THEN 'Submitted'
                        ELSE 'Saved' END                             AS [Status],
                   CONVERT(VARCHAR(19), ct.[SubmittedAt], 120)      AS [Submitted At],
                   CONVERT(VARCHAR(19), ct.[VoidedAt],    120)      AS [Voided At]
            FROM   [dbo].[CheckTransactions] ct
            LEFT JOIN [dbo].[Vendors] v ON v.[VendorID] = ct.[VendorID]
            WHERE  ct.[LocationID]  = @Loc
              AND  ct.[TransactionDate] BETWEEN @From AND @To
            ORDER  BY ct.[TransactionDate] DESC, ct.[CheckNumber];
            """, loc, from, to, ct);

    // ── Payroll ────────────────────────────────────────────────────────────────

    private Task<ReportResult> PayrollByPeriodAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT pr.[BatchName]                                    AS [Batch],
                   CONVERT(VARCHAR(10), pr.[PayPeriodStart], 101)   AS [Period Start],
                   CONVERT(VARCHAR(10), pr.[PayPeriodEnd],   101)   AS [Period End],
                   pr.[Status]                                       AS [Status],
                   pr.[GrandTotal]                                   AS [Grand Total]
            FROM   [dbo].[PayrollRuns] pr
            WHERE  pr.[LocationID]  = @Loc
              AND  pr.[Status]      = 'Submitted'
              AND  pr.[PayPeriodEnd] BETWEEN @From AND @To
            ORDER  BY pr.[PayPeriodEnd] DESC;
            """, loc, from, to, ct);

    private Task<ReportResult> PayrollByEmployeeAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT e.[Name]                                          AS [Employee],
                   SUM(CASE WHEN l.[PayType] = 'Hourly'
                            THEN l.[Quantity] ELSE 0 END)           AS [Hours],
                   SUM(l.[TotalAmount])                             AS [Total Earnings]
            FROM   [dbo].[PayrollRunLines] l
            JOIN   [dbo].[PayrollRuns]     pr ON pr.[RunID]      = l.[RunID]
            JOIN   [dbo].[Employees]       e  ON e.[EmployeeID]  = l.[EmployeeID]
            WHERE  pr.[LocationID]  = @Loc
              AND  pr.[Status]      = 'Submitted'
              AND  pr.[PayPeriodEnd] BETWEEN @From AND @To
            GROUP  BY e.[EmployeeID], e.[Name]
            ORDER  BY [Total Earnings] DESC;
            """, loc, from, to, ct);

    private Task<ReportResult> PayrollByRoleAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT jr.[Name]                                         AS [Job Role],
                   SUM(CASE WHEN l.[PayType] = 'Hourly'
                            THEN l.[Quantity] ELSE 0 END)           AS [Hours],
                   SUM(l.[TotalAmount])                             AS [Total Pay]
            FROM   [dbo].[PayrollRunLines] l
            JOIN   [dbo].[PayrollRuns] pr ON pr.[RunID]     = l.[RunID]
            JOIN   [dbo].[JobRoles]    jr ON jr.[JobRoleID] = l.[JobRoleID]
            WHERE  pr.[LocationID]  = @Loc
              AND  pr.[Status]      = 'Submitted'
              AND  pr.[PayPeriodEnd] BETWEEN @From AND @To
            GROUP  BY jr.[JobRoleID], jr.[Name]
            ORDER  BY [Total Pay] DESC;
            """, loc, from, to, ct);

    private async Task<ReportResult> PayrollYtdAsync(int loc, CancellationToken ct)
    {
        var from = new DateOnly(DateTime.Today.Year, 1, 1);
        var to   = DateOnly.FromDateTime(DateTime.Today);
        return await QueryAsync("""
            SELECT CONVERT(VARCHAR(10), pr.[PayPeriodEnd], 101)     AS [Period End],
                   pr.[BatchName]                                    AS [Batch],
                   pr.[GrandTotal]                                   AS [Grand Total],
                   SUM(pr.[GrandTotal]) OVER (
                       PARTITION BY pr.[LocationID]
                       ORDER BY pr.[PayPeriodEnd]
                       ROWS UNBOUNDED PRECEDING)                     AS [YTD Running Total]
            FROM   [dbo].[PayrollRuns] pr
            WHERE  pr.[LocationID]  = @Loc
              AND  pr.[Status]      = 'Submitted'
              AND  pr.[PayPeriodEnd] BETWEEN @From AND @To
            ORDER  BY pr.[PayPeriodEnd];
            """, loc, from, to, ct);
    }

    private Task<ReportResult> PayrollMonthlyAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT CAST(YEAR(pr.[PayPeriodEnd]) AS VARCHAR(4)) + '-'
                   + RIGHT('0' + CAST(MONTH(pr.[PayPeriodEnd]) AS VARCHAR(2)), 2) AS [Month],
                   COUNT(*)                                          AS [Runs],
                   SUM(pr.[GrandTotal])                             AS [Total Payroll]
            FROM   [dbo].[PayrollRuns] pr
            WHERE  pr.[LocationID]  = @Loc
              AND  pr.[Status]      = 'Submitted'
              AND  pr.[PayPeriodEnd] BETWEEN @From AND @To
            GROUP  BY YEAR(pr.[PayPeriodEnd]), MONTH(pr.[PayPeriodEnd])
            ORDER  BY YEAR(pr.[PayPeriodEnd]), MONTH(pr.[PayPeriodEnd]);
            """, loc, from, to, ct);

    private Task<ReportResult> PayrollRunDetailAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT CONVERT(VARCHAR(10), pr.[PayPeriodEnd], 101)     AS [Period End],
                   pr.[BatchName]                                    AS [Batch],
                   e.[Name]                                          AS [Employee],
                   jr.[Name]                                         AS [Job Role],
                   l.[PayType]                                       AS [Pay Type],
                   l.[Quantity]                                      AS [Qty/Hours],
                   l.[PayRate]                                       AS [Rate],
                   l.[TotalAmount]                                   AS [Total]
            FROM   [dbo].[PayrollRunLines] l
            JOIN   [dbo].[PayrollRuns]  pr ON pr.[RunID]      = l.[RunID]
            JOIN   [dbo].[Employees]    e  ON e.[EmployeeID]  = l.[EmployeeID]
            JOIN   [dbo].[JobRoles]     jr ON jr.[JobRoleID]  = l.[JobRoleID]
            WHERE  pr.[LocationID]  = @Loc
              AND  pr.[Status]      = 'Submitted'
              AND  pr.[PayPeriodEnd] BETWEEN @From AND @To
            ORDER  BY pr.[PayPeriodEnd], pr.[BatchName], e.[Name], jr.[Name];
            """, loc, from, to, ct);

    private Task<ReportResult> PayrollSubmissionLogAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            SELECT pr.[BatchName]                                    AS [Batch],
                   CONVERT(VARCHAR(10), pr.[PayPeriodStart], 101)   AS [Period Start],
                   CONVERT(VARCHAR(10), pr.[PayPeriodEnd],   101)   AS [Period End],
                   pr.[GrandTotal]                                   AS [Grand Total],
                   pr.[Status]                                       AS [Status],
                   CONVERT(VARCHAR(19), pr.[SubmittedAt], 120)      AS [Submitted At],
                   CONVERT(VARCHAR(19), pr.[VoidedAt],    120)      AS [Voided At]
            FROM   [dbo].[PayrollRuns] pr
            WHERE  pr.[LocationID]   = @Loc
              AND  pr.[PayPeriodEnd] BETWEEN @From AND @To
            ORDER  BY pr.[PayPeriodEnd] DESC;
            """, loc, from, to, ct);

    // ── Security ───────────────────────────────────────────────────────────────

    private async Task<ReportResult> SecurityUserLocationsAsync(CancellationToken ct)
    {
        var from = new DateOnly(2000, 1, 1);
        var to   = DateOnly.FromDateTime(DateTime.Today);
        return await QueryAsync("""
            SELECT u.[FullName]                                      AS [User],
                   u.[Username]                                      AS [Username],
                   u.[RoleType]                                      AS [Role],
                   CASE WHEN u.[IsActive] = 1 THEN 'Active' ELSE 'Inactive' END AS [Status],
                   ISNULL(l.[Code], '—')                            AS [Location Code],
                   ISNULL(l.[Name], '(All Locations — Administrator)') AS [Location]
            FROM   [dbo].[Users] u
            LEFT JOIN [dbo].[UserLocations] ul ON ul.[UserID]    = u.[UserID]
            LEFT JOIN [dbo].[Locations]     l  ON l.[LocationID] = ul.[LocationID]
            ORDER  BY u.[FullName], l.[Code];
            """, 0, from, to, ct);
    }

    private async Task<ReportResult> SecurityUserModulesAsync(CancellationToken ct)
    {
        var from = new DateOnly(2000, 1, 1);
        var to   = DateOnly.FromDateTime(DateTime.Today);
        return await QueryAsync("""
            SELECT u.[FullName]                                      AS [User],
                   u.[Username]                                      AS [Username],
                   u.[RoleType]                                      AS [Role],
                   CASE WHEN u.[IsActive] = 1 THEN 'Active' ELSE 'Inactive' END AS [Status],
                   m.[DisplayName]                                   AS [Module],
                   ISNULL(p.[PermissionLevel], 'None')              AS [Permission Level]
            FROM   [dbo].[Users]   u
            CROSS JOIN [dbo].[Modules] m
            LEFT  JOIN [dbo].[UserModulePermissions] p
                    ON p.[UserID]   = u.[UserID]
                   AND p.[ModuleID] = m.[ModuleID]
            WHERE  m.[IsActive] = 1
            ORDER  BY u.[FullName], m.[SortOrder], m.[DisplayName];
            """, 0, from, to, ct);
    }

    private async Task<ReportResult> SecurityUserSummaryAsync(CancellationToken ct)
    {
        var from = new DateOnly(2000, 1, 1);
        var to   = DateOnly.FromDateTime(DateTime.Today);
        return await QueryAsync("""
            SELECT u.[FullName]                                                  AS [User],
                   u.[Username]                                                  AS [Username],
                   u.[RoleType]                                                  AS [Role],
                   CASE WHEN u.[IsActive] = 1 THEN 'Active' ELSE 'Inactive' END AS [Status],
                   CONVERT(VARCHAR(19), u.[LastLoginAt], 120)                   AS [Last Login],
                   ISNULL(
                       STUFF((
                           SELECT ', ' + l2.[Code]
                           FROM   [dbo].[UserLocations] ul2
                           JOIN   [dbo].[Locations]     l2 ON l2.[LocationID] = ul2.[LocationID]
                           WHERE  ul2.[UserID] = u.[UserID]
                           ORDER  BY l2.[Code]
                           FOR XML PATH(''), TYPE).value('.','NVARCHAR(MAX)'), 1, 2, ''),
                   CASE WHEN u.[RoleType] = 'Administrator' THEN '(All)' ELSE '—' END
                   )                                                              AS [Locations],
                   ISNULL(
                       STUFF((
                           SELECT ', ' + m2.[DisplayName] + ' [' + p2.[PermissionLevel] + ']'
                           FROM   [dbo].[UserModulePermissions] p2
                           JOIN   [dbo].[Modules] m2 ON m2.[ModuleID] = p2.[ModuleID]
                           WHERE  p2.[UserID] = u.[UserID]
                             AND  p2.[PermissionLevel] <> 'None'
                           ORDER  BY m2.[SortOrder]
                           FOR XML PATH(''), TYPE).value('.','NVARCHAR(MAX)'), 1, 2, ''),
                   '—')                                                           AS [Module Permissions]
            FROM   [dbo].[Users] u
            ORDER  BY u.[FullName];
            """, 0, from, to, ct);
    }

    // ── Financial Overview ─────────────────────────────────────────────────────

    private async Task<ReportResult> FinancialOverviewAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        decimal eodSales  = await ScalarAsync(conn, """
            SELECT ISNULL(SUM(v.[Amount] * sec.[Multiplier]), 0)
            FROM   [dbo].[EodSalesEntries] e
            JOIN   [dbo].[EodSalesValues]  v   ON v.[EntryID]     = e.[EntryID]
            JOIN   [dbo].[EodRows]         r   ON r.[RowID]       = v.[RowID]
            JOIN   [dbo].[EodSections]     sec ON sec.[SectionID] = r.[SectionID]
            WHERE  e.[LocationID] = @Loc AND e.[IsSubmitted]=1 AND e.[IsVoided]=0
              AND  sec.[UseInEodSales]=1
              AND  e.[EntryDate] BETWEEN @From AND @To;
            """, loc, from, to, ct);

        decimal payables  = await ScalarAsync(conn, """
            SELECT ISNULL(SUM(ISNULL(li.[LineTotal],0)+ISNULL(h.[ShippingCharge],0)+ISNULL(h.[TaxAmount],0)),0)
            FROM   [dbo].[PayableHeaders] h
            LEFT JOIN (SELECT [PayableID],SUM([ExtendedPrice]) AS [LineTotal]
                       FROM [dbo].[PayableLineItems] GROUP BY [PayableID]) li
                ON li.[PayableID]=h.[PayableID]
            WHERE  h.[LocationID]=@Loc AND h.[Status]='Submitted'
              AND  h.[InvoiceDate] BETWEEN @From AND @To;
            """, loc, from, to, ct);

        decimal checks    = await ScalarAsync(conn, """
            SELECT ISNULL(SUM([Amount]),0)
            FROM   [dbo].[CheckTransactions]
            WHERE  [LocationID]=@Loc AND [IsSubmitted]=1 AND [IsVoided]=0
              AND  [TransactionDate] BETWEEN @From AND @To;
            """, loc, from, to, ct);

        decimal payroll   = await ScalarAsync(conn, """
            SELECT ISNULL(SUM([GrandTotal]),0)
            FROM   [dbo].[PayrollRuns]
            WHERE  [LocationID]=@Loc AND [Status]='Submitted'
              AND  [PayPeriodEnd] BETWEEN @From AND @To;
            """, loc, from, to, ct);

        var result = new ReportResult
        {
            Columns = ["Module", "Total"],
            Rows    =
            [
                ["EOD Sales",   eodSales],
                ["Payables",    payables],
                ["Checks",      checks],
                ["Payroll",     payroll],
                ["Net (Sales − Outflows)", eodSales - payables - checks - payroll],
            ]
        };
        return result;
    }

    private Task<ReportResult> FinancialCashFlowAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            WITH dates AS (
                SELECT DISTINCT [EntryDate] AS [D] FROM [dbo].[EodSalesEntries]
                WHERE [LocationID]=@Loc AND [EntryDate] BETWEEN @From AND @To
                UNION
                SELECT DISTINCT [InvoiceDate]     FROM [dbo].[PayableHeaders]
                WHERE [LocationID]=@Loc AND [InvoiceDate] BETWEEN @From AND @To
                UNION
                SELECT DISTINCT [TransactionDate] FROM [dbo].[CheckTransactions]
                WHERE [LocationID]=@Loc AND [TransactionDate] BETWEEN @From AND @To
            ),
            eod AS (
                SELECT e.[EntryDate], SUM(v.[Amount]*sec.[Multiplier]) AS [Sales]
                FROM [dbo].[EodSalesEntries] e
                JOIN [dbo].[EodSalesValues]  v   ON v.[EntryID]=e.[EntryID]
                JOIN [dbo].[EodRows]         r   ON r.[RowID]=v.[RowID]
                JOIN [dbo].[EodSections]     sec ON sec.[SectionID]=r.[SectionID]
                WHERE e.[LocationID]=@Loc AND e.[IsSubmitted]=1 AND e.[IsVoided]=0
                  AND sec.[UseInEodSales]=1
                GROUP BY e.[EntryDate]
            ),
            pay AS (
                SELECT h.[InvoiceDate],
                       SUM(ISNULL(li.[LineTotal],0)+ISNULL(h.[ShippingCharge],0)+ISNULL(h.[TaxAmount],0)) AS [Payables]
                FROM [dbo].[PayableHeaders] h
                LEFT JOIN (SELECT [PayableID],SUM([ExtendedPrice]) AS [LineTotal]
                           FROM [dbo].[PayableLineItems] GROUP BY [PayableID]) li
                    ON li.[PayableID]=h.[PayableID]
                WHERE h.[LocationID]=@Loc AND h.[Status]='Submitted'
                GROUP BY h.[InvoiceDate]
            ),
            chk AS (
                SELECT [TransactionDate], SUM([Amount]) AS [Checks]
                FROM [dbo].[CheckTransactions]
                WHERE [LocationID]=@Loc AND [IsSubmitted]=1 AND [IsVoided]=0
                GROUP BY [TransactionDate]
            )
            SELECT CONVERT(VARCHAR(10), d.[D], 101)     AS [Date],
                   ISNULL(eod.[Sales],    0)            AS [EOD Sales],
                   ISNULL(pay.[Payables], 0)            AS [Payables],
                   ISNULL(chk.[Checks],   0)            AS [Checks],
                   ISNULL(eod.[Sales],0)
                     - ISNULL(pay.[Payables],0)
                     - ISNULL(chk.[Checks],0)           AS [Net]
            FROM   dates d
            LEFT JOIN eod ON eod.[EntryDate]      = d.[D]
            LEFT JOIN pay ON pay.[InvoiceDate]    = d.[D]
            LEFT JOIN chk ON chk.[TransactionDate]= d.[D]
            ORDER  BY d.[D];
            """, loc, from, to, ct);

    private Task<ReportResult> FinancialMonthlyAsync(int loc, DateOnly from, DateOnly to, CancellationToken ct)
        => QueryAsync("""
            WITH months AS (
                SELECT DISTINCT YEAR([EntryDate]) AS [Y], MONTH([EntryDate]) AS [M]
                FROM [dbo].[EodSalesEntries] WHERE [LocationID]=@Loc AND [EntryDate] BETWEEN @From AND @To
                UNION
                SELECT DISTINCT YEAR([InvoiceDate]),MONTH([InvoiceDate])
                FROM [dbo].[PayableHeaders]      WHERE [LocationID]=@Loc AND [InvoiceDate] BETWEEN @From AND @To
                UNION
                SELECT DISTINCT YEAR([TransactionDate]),MONTH([TransactionDate])
                FROM [dbo].[CheckTransactions]   WHERE [LocationID]=@Loc AND [TransactionDate] BETWEEN @From AND @To
                UNION
                SELECT DISTINCT YEAR([PayPeriodEnd]),MONTH([PayPeriodEnd])
                FROM [dbo].[PayrollRuns]         WHERE [LocationID]=@Loc AND [PayPeriodEnd] BETWEEN @From AND @To
            ),
            eod AS (
                SELECT YEAR(e.[EntryDate]) AS Y, MONTH(e.[EntryDate]) AS M,
                       SUM(v.[Amount]*sec.[Multiplier]) AS [Sales]
                FROM [dbo].[EodSalesEntries] e
                JOIN [dbo].[EodSalesValues]  v   ON v.[EntryID]=e.[EntryID]
                JOIN [dbo].[EodRows]         r   ON r.[RowID]=v.[RowID]
                JOIN [dbo].[EodSections]     sec ON sec.[SectionID]=r.[SectionID]
                WHERE e.[LocationID]=@Loc AND e.[IsSubmitted]=1 AND e.[IsVoided]=0
                  AND sec.[UseInEodSales]=1
                GROUP BY YEAR(e.[EntryDate]),MONTH(e.[EntryDate])
            ),
            pay AS (
                SELECT YEAR(h.[InvoiceDate]) AS Y, MONTH(h.[InvoiceDate]) AS M,
                       SUM(ISNULL(li.[LineTotal],0)+ISNULL(h.[ShippingCharge],0)+ISNULL(h.[TaxAmount],0)) AS [Payables]
                FROM [dbo].[PayableHeaders] h
                LEFT JOIN (SELECT [PayableID],SUM([ExtendedPrice]) AS [LineTotal]
                           FROM [dbo].[PayableLineItems] GROUP BY [PayableID]) li
                    ON li.[PayableID]=h.[PayableID]
                WHERE h.[LocationID]=@Loc AND h.[Status]='Submitted'
                GROUP BY YEAR(h.[InvoiceDate]),MONTH(h.[InvoiceDate])
            ),
            chk AS (
                SELECT YEAR([TransactionDate]) AS Y, MONTH([TransactionDate]) AS M,
                       SUM([Amount]) AS [Checks]
                FROM [dbo].[CheckTransactions]
                WHERE [LocationID]=@Loc AND [IsSubmitted]=1 AND [IsVoided]=0
                GROUP BY YEAR([TransactionDate]),MONTH([TransactionDate])
            ),
            prl AS (
                SELECT YEAR([PayPeriodEnd]) AS Y, MONTH([PayPeriodEnd]) AS M,
                       SUM([GrandTotal]) AS [Payroll]
                FROM [dbo].[PayrollRuns]
                WHERE [LocationID]=@Loc AND [Status]='Submitted'
                GROUP BY YEAR([PayPeriodEnd]),MONTH([PayPeriodEnd])
            )
            SELECT CAST(mo.[Y] AS VARCHAR(4)) + '-'
                   + RIGHT('0'+CAST(mo.[M] AS VARCHAR(2)),2)   AS [Month],
                   ISNULL(eod.[Sales],    0)                   AS [EOD Sales],
                   ISNULL(pay.[Payables], 0)                   AS [Payables],
                   ISNULL(chk.[Checks],   0)                   AS [Checks],
                   ISNULL(prl.[Payroll],  0)                   AS [Payroll]
            FROM   months mo
            LEFT JOIN eod ON eod.Y=mo.Y AND eod.M=mo.M
            LEFT JOIN pay ON pay.Y=mo.Y AND pay.M=mo.M
            LEFT JOIN chk ON chk.Y=mo.Y AND chk.M=mo.M
            LEFT JOIN prl ON prl.Y=mo.Y AND prl.M=mo.M
            ORDER  BY mo.[Y], mo.[M];
            """, loc, from, to, ct);

    private async Task<ReportResult> FinancialAnnualAsync(int loc, CancellationToken ct)
    {
        var from = DateOnly.MinValue;
        var to   = DateOnly.FromDateTime(DateTime.Today);
        return await QueryAsync("""
            WITH years AS (
                SELECT DISTINCT YEAR([EntryDate]) AS [Y]
                FROM [dbo].[EodSalesEntries] WHERE [LocationID]=@Loc
                UNION
                SELECT DISTINCT YEAR([InvoiceDate])
                FROM [dbo].[PayableHeaders] WHERE [LocationID]=@Loc
                UNION
                SELECT DISTINCT YEAR([TransactionDate])
                FROM [dbo].[CheckTransactions] WHERE [LocationID]=@Loc
                UNION
                SELECT DISTINCT YEAR([PayPeriodEnd])
                FROM [dbo].[PayrollRuns] WHERE [LocationID]=@Loc
            ),
            eod AS (
                SELECT YEAR(e.[EntryDate]) AS Y, SUM(v.[Amount]*sec.[Multiplier]) AS [Sales]
                FROM [dbo].[EodSalesEntries] e
                JOIN [dbo].[EodSalesValues]  v   ON v.[EntryID]=e.[EntryID]
                JOIN [dbo].[EodRows]         r   ON r.[RowID]=v.[RowID]
                JOIN [dbo].[EodSections]     sec ON sec.[SectionID]=r.[SectionID]
                WHERE e.[LocationID]=@Loc AND e.[IsSubmitted]=1 AND e.[IsVoided]=0
                  AND sec.[UseInEodSales]=1
                GROUP BY YEAR(e.[EntryDate])
            ),
            pay AS (
                SELECT YEAR(h.[InvoiceDate]) AS Y,
                       SUM(ISNULL(li.[LineTotal],0)+ISNULL(h.[ShippingCharge],0)+ISNULL(h.[TaxAmount],0)) AS [Payables]
                FROM [dbo].[PayableHeaders] h
                LEFT JOIN (SELECT [PayableID],SUM([ExtendedPrice]) AS [LineTotal]
                           FROM [dbo].[PayableLineItems] GROUP BY [PayableID]) li
                    ON li.[PayableID]=h.[PayableID]
                WHERE h.[LocationID]=@Loc AND h.[Status]='Submitted'
                GROUP BY YEAR(h.[InvoiceDate])
            ),
            chk AS (
                SELECT YEAR([TransactionDate]) AS Y, SUM([Amount]) AS [Checks]
                FROM [dbo].[CheckTransactions]
                WHERE [LocationID]=@Loc AND [IsSubmitted]=1 AND [IsVoided]=0
                GROUP BY YEAR([TransactionDate])
            ),
            prl AS (
                SELECT YEAR([PayPeriodEnd]) AS Y, SUM([GrandTotal]) AS [Payroll]
                FROM [dbo].[PayrollRuns]
                WHERE [LocationID]=@Loc AND [Status]='Submitted'
                GROUP BY YEAR([PayPeriodEnd])
            )
            SELECT CAST(yr.[Y] AS VARCHAR(4))  AS [Year],
                   ISNULL(eod.[Sales],    0)   AS [EOD Sales],
                   ISNULL(pay.[Payables], 0)   AS [Payables],
                   ISNULL(chk.[Checks],   0)   AS [Checks],
                   ISNULL(prl.[Payroll],  0)   AS [Payroll]
            FROM   years yr
            LEFT JOIN eod ON eod.Y=yr.Y
            LEFT JOIN pay ON pay.Y=yr.Y
            LEFT JOIN chk ON chk.Y=yr.Y
            LEFT JOIN prl ON prl.Y=yr.Y
            ORDER  BY yr.[Y];
            """, loc, from, to, ct);
    }

    // ── Excel export ───────────────────────────────────────────────────────────

    public static byte[] ExportToExcel(ReportResult result, string sheetTitle)
    {
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.Worksheets.Add(sheetTitle.Length > 31 ? sheetTitle[..31] : sheetTitle);

        // Header row
        for (int c = 0; c < result.Columns.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = result.Columns[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#2B6B35");
            cell.Style.Font.FontColor       = ClosedXML.Excel.XLColor.White;
            cell.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
        }

        // Data rows
        for (int r = 0; r < result.Rows.Count; r++)
        {
            var row = result.Rows[r];
            for (int c = 0; c < row.Length; c++)
            {
                var cell = ws.Cell(r + 2, c + 1);
                switch (row[c])
                {
                    case decimal d: cell.Value = d; cell.Style.NumberFormat.Format = "#,##0.00"; break;
                    case int    i: cell.Value = i; break;
                    case long   l: cell.Value = l; break;
                    case null:     cell.Value = string.Empty; break;
                    default:       cell.Value = row[c]?.ToString() ?? string.Empty; break;
                }
            }
            // Alternate row shading
            if (r % 2 == 1)
                ws.Row(r + 2).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#F0F7F0");
        }

        ws.Columns().AdjustToContents(8, 60);
        ws.SheetView.FreezeRows(1);

        using var ms = new System.IO.MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── Shared query helpers ───────────────────────────────────────────────────

    private async Task<ReportResult> QueryAsync(string sql, int loc, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows    = new List<object?[]>();
        string[]? cols = null;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Loc",  loc);
        cmd.Parameters.AddWithValue("@From", from == DateOnly.MinValue ? (object)DBNull.Value : from.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@To",   to   == DateOnly.MaxValue ? (object)DBNull.Value : to.ToDateTime(TimeOnly.MaxValue));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        cols = Enumerable.Range(0, r.FieldCount).Select(i => r.GetName(i)).ToArray();
        while (await r.ReadAsync(ct))
        {
            var row = new object?[r.FieldCount];
            for (int i = 0; i < r.FieldCount; i++)
                row[i] = r.IsDBNull(i) ? null : r.GetValue(i);
            rows.Add(row);
        }
        return new ReportResult { Columns = cols ?? [], Rows = rows };
    }

    private static async Task<decimal> ScalarAsync(SqlConnection conn, string sql,
                                                    int loc, DateOnly from, DateOnly to, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Loc",  loc);
        cmd.Parameters.AddWithValue("@From", from.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@To",   to.ToDateTime(TimeOnly.MaxValue));
        var v = await cmd.ExecuteScalarAsync(ct);
        return v is decimal d ? d : 0m;
    }
}
