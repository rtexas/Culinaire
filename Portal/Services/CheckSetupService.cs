using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class CheckSetupService
{
    private readonly string _cs;
    public CheckSetupService(string connectionString) => _cs = connectionString;

    // ── Vendors ───────────────────────────────────────────────────────────────

    public async Task<List<CheckSetupVendor>> GetVendorsAsync(int locationId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT csv.[CheckSetupVendorID], csv.[LocationID], csv.[VendorID], csv.[IsActive],
                   v.[VendorCode], v.[Name]
            FROM   [dbo].[CheckSetupVendors] csv
            JOIN   [dbo].[Vendors] v ON v.[VendorID] = csv.[VendorID]
            WHERE  csv.[LocationID] = @LocID
            ORDER  BY v.[Name];
            """;
        var list = new List<CheckSetupVendor>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@LocID", locationId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new CheckSetupVendor
            {
                CheckSetupVendorID = r.GetInt32(0),
                LocationID         = r.GetInt32(1),
                VendorID           = r.GetInt32(2),
                IsActive           = r.GetBoolean(3),
                VendorCode         = r.GetString(4),
                VendorName         = r.GetString(5),
            });
        return list;
    }

    public async Task AddVendorAsync(int locationId, int vendorId, CancellationToken ct = default)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM [dbo].[CheckSetupVendors] WHERE [LocationID]=@L AND [VendorID]=@V)
                INSERT INTO [dbo].[CheckSetupVendors]([LocationID],[VendorID]) VALUES(@L,@V);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@L", locationId);
        cmd.Parameters.AddWithValue("@V", vendorId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveVendorAsync(int checkSetupVendorId, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("DELETE FROM [dbo].[CheckSetupVendors] WHERE [CheckSetupVendorID]=@ID;", conn);
        cmd.Parameters.AddWithValue("@ID", checkSetupVendorId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Accounts ─────────────────────────────────────────────────────────────

    public async Task<List<CheckSetupAccount>> GetAccountsAsync(int locationId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT csa.[CheckSetupAccountID], csa.[LocationID], csa.[AccountID], csa.[IsActive],
                   a.[AccountName], ISNULL(a.[FullAccountString],'')
            FROM   [dbo].[CheckSetupAccounts] csa
            JOIN   [dbo].[ChartOfAccounts] a ON a.[AccountID] = csa.[AccountID]
            WHERE  csa.[LocationID] = @LocID
            ORDER  BY a.[AccountName];
            """;
        var list = new List<CheckSetupAccount>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@LocID", locationId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new CheckSetupAccount
            {
                CheckSetupAccountID = r.GetInt32(0),
                LocationID          = r.GetInt32(1),
                AccountID           = r.GetInt32(2),
                IsActive            = r.GetBoolean(3),
                AccountName         = r.GetString(4),
                FullAccountString   = r.GetString(5),
            });
        return list;
    }

    public async Task AddAccountAsync(int locationId, int accountId, CancellationToken ct = default)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM [dbo].[CheckSetupAccounts] WHERE [LocationID]=@L AND [AccountID]=@A)
                INSERT INTO [dbo].[CheckSetupAccounts]([LocationID],[AccountID]) VALUES(@L,@A);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@L", locationId);
        cmd.Parameters.AddWithValue("@A", accountId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveAccountAsync(int checkSetupAccountId, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("DELETE FROM [dbo].[CheckSetupAccounts] WHERE [CheckSetupAccountID]=@ID;", conn);
        cmd.Parameters.AddWithValue("@ID", checkSetupAccountId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
