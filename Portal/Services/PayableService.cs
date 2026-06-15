using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class PayableService
{
    private readonly string _cs;
    public PayableService(string connectionString) => _cs = connectionString;

    private const string HeaderSelect = """
        SELECT h.[PayableID], h.[VendorID], v.[Name] AS VendorName,
               h.[InvoiceNumber], h.[InvoiceDate], h.[DueDate],
               h.[ShippingMethodID], ISNULL(sm.[Name],'') AS ShippingMethodName,
               ISNULL(h.[ShippingCharge],0), ISNULL(h.[TaxAmount],0),
               ISNULL(h.[Notes],''), h.[Status],
               ISNULL((SELECT SUM(l.[ExtendedPrice]) FROM [dbo].[PayableLineItems] l
                        WHERE l.[PayableID] = h.[PayableID]), 0) AS Subtotal,
               h.[CreatedAt], h.[UpdatedAt], h.[LocationID],
               h.[DueDateTermID], ISNULL(pt.[Name],'') AS DueDateTermName
        FROM   [dbo].[PayableHeaders] h
        JOIN   [dbo].[Vendors]         v  ON v.[VendorID]         = h.[VendorID]
        LEFT JOIN [dbo].[ShippingMethods] sm ON sm.[ShippingMethodID] = h.[ShippingMethodID]
        LEFT JOIN [dbo].[PayableTerms]    pt ON pt.[PayableTermID]   = h.[DueDateTermID]
        """;

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<List<PayableHeader>> GetAllAsync(CancellationToken ct = default)
    {
        var sql = HeaderSelect + " ORDER BY h.[InvoiceDate] DESC, h.[PayableID] DESC;";
        var list = new List<PayableHeader>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        await using var r   = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(MapHeader(r));
        return list;
    }

    public async Task<List<PayableHeader>> GetRecentAsync(int locationId, int top = 20, CancellationToken ct = default)
    {
        var sql = $"SELECT TOP(@Top) h.[PayableID], h.[VendorID], v.[Name] AS VendorName," +
                  " h.[InvoiceNumber], h.[InvoiceDate], h.[DueDate]," +
                  " h.[ShippingMethodID], ISNULL(sm.[Name],'') AS ShippingMethodName," +
                  " ISNULL(h.[ShippingCharge],0), ISNULL(h.[TaxAmount],0)," +
                  " ISNULL(h.[Notes],''), h.[Status]," +
                  " ISNULL((SELECT SUM(l.[ExtendedPrice]) FROM [dbo].[PayableLineItems] l WHERE l.[PayableID]=h.[PayableID]),0) AS Subtotal," +
                  " h.[CreatedAt], h.[UpdatedAt], h.[LocationID]," +
                  " h.[DueDateTermID], ISNULL(pt.[Name],'') AS DueDateTermName" +
                  " FROM [dbo].[PayableHeaders] h" +
                  " JOIN [dbo].[Vendors] v ON v.[VendorID]=h.[VendorID]" +
                  " LEFT JOIN [dbo].[ShippingMethods] sm ON sm.[ShippingMethodID]=h.[ShippingMethodID]" +
                  " LEFT JOIN [dbo].[PayableTerms] pt ON pt.[PayableTermID]=h.[DueDateTermID]" +
                  " WHERE (h.[LocationID]=@LocID OR (h.[LocationID] IS NULL AND @LocID=0))" +
                  " ORDER BY h.[InvoiceDate] DESC, h.[PayableID] DESC;";
        var list = new List<PayableHeader>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Top",   top);
        cmd.Parameters.AddWithValue("@LocID", locationId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(MapHeader(r));
        return list;
    }

    public Task SubmitAsync(int id, CancellationToken ct = default) =>
        UpdateStatusAsync(id, "Submitted", ct);

    public Task VoidAsync(int id, CancellationToken ct = default) =>
        UpdateStatusAsync(id, "Voided", ct);

    public async Task<PayableHeader?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var sql = HeaderSelect + " WHERE h.[PayableID]=@ID;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? MapHeader(r) : null;
    }

    public async Task<List<PayableLineItem>> GetLineItemsAsync(int payableId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT [LineItemID],[PayableID],[LineNumber],[ItemID],
                   [Description],[Quantity],[UnitPrice],[ExtendedPrice]
            FROM   [dbo].[PayableLineItems]
            WHERE  [PayableID]=@ID
            ORDER  BY [LineNumber];
            """;
        var list = new List<PayableLineItem>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ID", payableId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(MapLine(r));
        return list;
    }

    // ── Save (header + lines in one transaction) ──────────────────────────────

    public async Task<int> SavePayableAsync(PayableHeader header,
        List<PayableLineItem> lines, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);
        try
        {
            int payableId = header.PayableID == 0
                ? await InsertHeaderAsync(conn, tx, header, ct)
                : await UpdateHeaderAsync(conn, tx, header, ct);

            await DeleteLineItemsAsync(conn, tx, payableId, ct);
            for (int i = 0; i < lines.Count; i++)
                await InsertLineItemAsync(conn, tx, payableId, i + 1, lines[i], ct);

            await tx.CommitAsync(ct);
            return payableId;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "DELETE FROM [dbo].[PayableHeaders] WHERE [PayableID]=@ID;", conn);
        cmd.Parameters.AddWithValue("@ID", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateStatusAsync(int id, string status, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "UPDATE [dbo].[PayableHeaders] SET [Status]=@Status,[UpdatedAt]=GETDATE() WHERE [PayableID]=@ID;", conn);
        cmd.Parameters.AddWithValue("@ID",     id);
        cmd.Parameters.AddWithValue("@Status", status);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static async Task<int> InsertHeaderAsync(SqlConnection conn, SqlTransaction tx,
        PayableHeader h, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO [dbo].[PayableHeaders]
                ([VendorID],[InvoiceNumber],[InvoiceDate],[DueDate],[DueDateTermID],
                 [ShippingMethodID],[ShippingCharge],[TaxAmount],[Notes],[Status],[LocationID])
            OUTPUT INSERTED.[PayableID]
            VALUES(@VendorID,@InvNum,@InvDate,@DueDate,@DueDateTermID,
                   @ShipMethodID,@ShipCharge,@Tax,@Notes,@Status,@LocID);
            """;
        await using var cmd = new SqlCommand(sql, conn, tx);
        BindHeaderParams(cmd, h);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private static async Task<int> UpdateHeaderAsync(SqlConnection conn, SqlTransaction tx,
        PayableHeader h, CancellationToken ct)
    {
        const string sql = """
            UPDATE [dbo].[PayableHeaders]
            SET [VendorID]=@VendorID,[InvoiceNumber]=@InvNum,[InvoiceDate]=@InvDate,
                [DueDate]=@DueDate,[DueDateTermID]=@DueDateTermID,
                [ShippingMethodID]=@ShipMethodID,
                [ShippingCharge]=@ShipCharge,[TaxAmount]=@Tax,
                [Notes]=@Notes,[Status]=@Status,[LocationID]=@LocID,[UpdatedAt]=GETDATE()
            WHERE [PayableID]=@PayableID;
            """;
        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@PayableID", h.PayableID);
        BindHeaderParams(cmd, h);
        await cmd.ExecuteNonQueryAsync(ct);
        return h.PayableID;
    }

    private static void BindHeaderParams(SqlCommand cmd, PayableHeader h)
    {
        cmd.Parameters.AddWithValue("@VendorID",      h.VendorID);
        cmd.Parameters.AddWithValue("@InvNum",        h.InvoiceNumber.Trim());
        cmd.Parameters.AddWithValue("@InvDate",       h.InvoiceDate);
        cmd.Parameters.AddWithValue("@DueDate",       (object?)h.DueDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DueDateTermID", (object?)h.DueDateTermID ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ShipMethodID",  (object?)h.ShippingMethodID ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ShipCharge",    h.ShippingCharge);
        cmd.Parameters.AddWithValue("@Tax",           h.TaxAmount);
        cmd.Parameters.AddWithValue("@Notes",         (object?)(string.IsNullOrWhiteSpace(h.Notes) ? null : h.Notes) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status",        h.Status);
        cmd.Parameters.AddWithValue("@LocID",         (object?)h.LocationID ?? DBNull.Value);
    }

    private static async Task DeleteLineItemsAsync(SqlConnection conn, SqlTransaction tx,
        int payableId, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(
            "DELETE FROM [dbo].[PayableLineItems] WHERE [PayableID]=@ID;", conn, tx);
        cmd.Parameters.AddWithValue("@ID", payableId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertLineItemAsync(SqlConnection conn, SqlTransaction tx,
        int payableId, int lineNumber, PayableLineItem line, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO [dbo].[PayableLineItems]
                ([PayableID],[LineNumber],[ItemID],[Description],[Quantity],[UnitPrice],[ExtendedPrice])
            VALUES(@PayableID,@LineNum,@ItemID,@Desc,@Qty,@Price,@Ext);
            """;
        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@PayableID", payableId);
        cmd.Parameters.AddWithValue("@LineNum",   lineNumber);
        cmd.Parameters.AddWithValue("@ItemID",    (object?)line.ItemID ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Desc",      line.Description.Trim());
        cmd.Parameters.AddWithValue("@Qty",       line.Quantity);
        cmd.Parameters.AddWithValue("@Price",     line.UnitPrice);
        cmd.Parameters.AddWithValue("@Ext",       Math.Round(line.Quantity * line.UnitPrice, 4));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static PayableHeader MapHeader(SqlDataReader r) => new()
    {
        PayableID          = r.GetInt32(0),
        VendorID           = r.GetInt32(1),
        VendorName         = r.GetString(2),
        InvoiceNumber      = r.GetString(3),
        InvoiceDate        = r.GetDateTime(4),
        DueDate            = r.IsDBNull(5) ? null : r.GetDateTime(5),
        ShippingMethodID   = r.IsDBNull(6) ? null : r.GetInt32(6),
        ShippingMethodName = r.GetString(7),
        ShippingCharge     = r.GetDecimal(8),
        TaxAmount          = r.GetDecimal(9),
        Notes              = r.GetString(10),
        Status             = r.GetString(11),
        Subtotal           = r.GetDecimal(12),
        CreatedAt          = r.GetDateTime(13),
        UpdatedAt          = r.GetDateTime(14),
        LocationID         = r.IsDBNull(15) ? null : r.GetInt32(15),
        DueDateTermID      = r.IsDBNull(16) ? null : r.GetInt32(16),
        DueDateTermName    = r.GetString(17),
    };

    private static PayableLineItem MapLine(SqlDataReader r) => new()
    {
        LineItemID    = r.GetInt32(0),
        PayableID     = r.GetInt32(1),
        LineNumber    = r.GetInt32(2),
        ItemID        = r.IsDBNull(3) ? null : r.GetInt32(3),
        Description   = r.GetString(4),
        Quantity      = r.GetDecimal(5),
        UnitPrice     = r.GetDecimal(6),
        ExtendedPrice = r.GetDecimal(7),
    };
}
