using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class CheckService
{
    private readonly string _cs;
    public CheckService(string connectionString) => _cs = connectionString;

    public async Task<int> GetNextCheckNumberAsync(int locationId, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "SELECT ISNULL(MAX([CheckNumber]),0)+1 FROM [dbo].[CheckTransactions] WHERE [LocationID]=@L;", conn);
        cmd.Parameters.AddWithValue("@L", locationId);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<List<CheckTransaction>> GetRecentAsync(int locationId, int top = 20, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP(@Top)
                   ct.[CheckTransactionID], ct.[LocationID], ct.[CheckNumber], ct.[TransactionDate],
                   ct.[VendorID], ct.[IsManualVendor],
                   ct.[ManualVendorName], ct.[ManualVendorAddress1], ct.[ManualVendorAddress2],
                   ct.[ManualVendorCity], ct.[ManualVendorState], ct.[ManualVendorZip],
                   ct.[Amount], ct.[Memo], ct.[ExpenseAccountID],
                   ct.[IsSubmitted], ct.[SubmittedAt], ct.[SubmittedByUserID],
                   ct.[IsVoided],   ct.[VoidedAt],    ct.[VoidedByUserID],
                   ct.[CreatedByUserID], ct.[CreatedAt],
                   ISNULL(v.[Name],''), ISNULL(a.[AccountName],'')
            FROM   [dbo].[CheckTransactions] ct
            LEFT JOIN [dbo].[Vendors]         v ON v.[VendorID]  = ct.[VendorID]
            LEFT JOIN [dbo].[ChartOfAccounts] a ON a.[AccountID] = ct.[ExpenseAccountID]
            WHERE  ct.[LocationID] = @L
            ORDER  BY ct.[TransactionDate] DESC, ct.[CheckNumber] DESC;
            """;
        var list = new List<CheckTransaction>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Top", top);
        cmd.Parameters.AddWithValue("@L", locationId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(Map(r));
        return list;
    }

    public async Task<int> InsertAsync(CheckTransaction t, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [dbo].[CheckTransactions]
                ([LocationID],[CheckNumber],[TransactionDate],[VendorID],[IsManualVendor],
                 [ManualVendorName],[ManualVendorAddress1],[ManualVendorAddress2],
                 [ManualVendorCity],[ManualVendorState],[ManualVendorZip],
                 [Amount],[Memo],[ExpenseAccountID],[IsSubmitted],[SubmittedAt],[SubmittedByUserID],[CreatedByUserID])
            OUTPUT INSERTED.[CheckTransactionID]
            VALUES(@Loc,@Num,@Date,@Vid,@Manual,
                   @MVName,@MVAddr1,@MVAddr2,@MVCity,@MVState,@MVZip,
                   @Amt,@Memo,@AcctID,@Submitted,@SubAt,@SubUID,@UID);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        Bind(cmd, t);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateAsync(CheckTransaction t, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[CheckTransactions]
            SET [TransactionDate]=@Date,[VendorID]=@Vid,[IsManualVendor]=@Manual,
                [ManualVendorName]=@MVName,[ManualVendorAddress1]=@MVAddr1,[ManualVendorAddress2]=@MVAddr2,
                [ManualVendorCity]=@MVCity,[ManualVendorState]=@MVState,[ManualVendorZip]=@MVZip,
                [Amount]=@Amt,[Memo]=@Memo,[ExpenseAccountID]=@AcctID
            WHERE [CheckTransactionID]=@ID AND [IsSubmitted]=0 AND [IsVoided]=0;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        static object N(string? s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : s;
        cmd.Parameters.AddWithValue("@ID",     t.CheckTransactionID);
        cmd.Parameters.AddWithValue("@Date",   t.TransactionDate.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@Vid",    (object?)t.VendorID ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Manual", t.IsManualVendor);
        cmd.Parameters.AddWithValue("@MVName", N(t.ManualVendorName));
        cmd.Parameters.AddWithValue("@MVAddr1",N(t.ManualVendorAddress1));
        cmd.Parameters.AddWithValue("@MVAddr2",N(t.ManualVendorAddress2));
        cmd.Parameters.AddWithValue("@MVCity", N(t.ManualVendorCity));
        cmd.Parameters.AddWithValue("@MVState",N(t.ManualVendorState));
        cmd.Parameters.AddWithValue("@MVZip",  N(t.ManualVendorZip));
        cmd.Parameters.AddWithValue("@Amt",    t.Amount);
        cmd.Parameters.AddWithValue("@Memo",   N(t.Memo));
        cmd.Parameters.AddWithValue("@AcctID", (object?)t.ExpenseAccountID ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SubmitAsync(int checkTransactionId, int userId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[CheckTransactions]
            SET    [IsSubmitted]=1, [SubmittedAt]=GETDATE(), [SubmittedByUserID]=@UID
            WHERE  [CheckTransactionID]=@ID AND [IsSubmitted]=0 AND [IsVoided]=0;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID",  checkTransactionId);
        cmd.Parameters.AddWithValue("@UID", userId > 0 ? userId : DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task VoidAsync(int checkTransactionId, int userId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [dbo].[CheckTransactions]
            SET    [IsVoided]=1, [VoidedAt]=GETDATE(), [VoidedByUserID]=@UID
            WHERE  [CheckTransactionID]=@ID AND [IsSubmitted]=0 AND [IsVoided]=0;
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID",  checkTransactionId);
        cmd.Parameters.AddWithValue("@UID", userId > 0 ? userId : DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void Bind(SqlCommand cmd, CheckTransaction t)
    {
        static object N(string? s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : s;
        cmd.Parameters.AddWithValue("@Loc",      t.LocationID);
        cmd.Parameters.AddWithValue("@Num",      t.CheckNumber);
        cmd.Parameters.AddWithValue("@Date",     t.TransactionDate.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@Vid",      (object?)t.VendorID ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Manual",   t.IsManualVendor);
        cmd.Parameters.AddWithValue("@MVName",   N(t.ManualVendorName));
        cmd.Parameters.AddWithValue("@MVAddr1",  N(t.ManualVendorAddress1));
        cmd.Parameters.AddWithValue("@MVAddr2",  N(t.ManualVendorAddress2));
        cmd.Parameters.AddWithValue("@MVCity",   N(t.ManualVendorCity));
        cmd.Parameters.AddWithValue("@MVState",  N(t.ManualVendorState));
        cmd.Parameters.AddWithValue("@MVZip",    N(t.ManualVendorZip));
        cmd.Parameters.AddWithValue("@Amt",      t.Amount);
        cmd.Parameters.AddWithValue("@Memo",     N(t.Memo));
        cmd.Parameters.AddWithValue("@AcctID",   (object?)t.ExpenseAccountID ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Submitted", t.IsSubmitted);
        cmd.Parameters.AddWithValue("@SubAt",    (object?)t.SubmittedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SubUID",   (object?)t.SubmittedByUserID ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UID",      (object?)t.CreatedByUserID ?? DBNull.Value);
    }

    private static CheckTransaction Map(SqlDataReader r) => new()
    {
        CheckTransactionID   = r.GetInt32(0),
        LocationID           = r.GetInt32(1),
        CheckNumber          = r.GetInt32(2),
        TransactionDate      = DateOnly.FromDateTime(r.GetDateTime(3)),
        VendorID             = r.IsDBNull(4)  ? null : r.GetInt32(4),
        IsManualVendor       = r.GetBoolean(5),
        ManualVendorName     = r.IsDBNull(6)  ? "" : r.GetString(6),
        ManualVendorAddress1 = r.IsDBNull(7)  ? "" : r.GetString(7),
        ManualVendorAddress2 = r.IsDBNull(8)  ? "" : r.GetString(8),
        ManualVendorCity     = r.IsDBNull(9)  ? "" : r.GetString(9),
        ManualVendorState    = r.IsDBNull(10) ? "" : r.GetString(10),
        ManualVendorZip      = r.IsDBNull(11) ? "" : r.GetString(11),
        Amount               = r.GetDecimal(12),
        Memo                 = r.IsDBNull(13) ? "" : r.GetString(13),
        ExpenseAccountID     = r.IsDBNull(14) ? null : r.GetInt32(14),
        IsSubmitted          = r.GetBoolean(15),
        SubmittedAt          = r.IsDBNull(16) ? null : r.GetDateTime(16),
        SubmittedByUserID    = r.IsDBNull(17) ? null : r.GetInt32(17),
        IsVoided             = r.GetBoolean(18),
        VoidedAt             = r.IsDBNull(19) ? null : r.GetDateTime(19),
        VoidedByUserID       = r.IsDBNull(20) ? null : r.GetInt32(20),
        CreatedByUserID      = r.IsDBNull(21) ? null : r.GetInt32(21),
        CreatedAt            = r.GetDateTime(22),
        VendorName           = r.GetString(23),
        AccountName          = r.GetString(24),
    };
}
