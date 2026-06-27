using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class AccountTypeService : NameDescServiceBase<AccountType>
{
    public AccountTypeService(string connectionString) : base(connectionString) { }

    protected override string Table   => "AccountTypes";
    protected override string IdCol   => "TypeID";
    protected override string OrderBy => "Name";

    protected override AccountType Map(SqlDataReader r) => new()
    {
        TypeID      = r.GetInt32(0),
        Name        = r.GetString(1),
        Description = r.IsDBNull(2) ? string.Empty : r.GetString(2),
        IsActive    = r.GetBoolean(3),
        CreatedAt   = r.GetDateTime(4),
    };

    protected override void BindCreate(SqlCommand cmd, AccountType item)
    {
        cmd.Parameters.AddWithValue("@Name", item.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc", (object?)ImportHelper.NullIfEmpty(item.Description) ?? DBNull.Value);
    }

    protected override void BindUpdate(SqlCommand cmd, AccountType item)
    {
        cmd.Parameters.AddWithValue("@ID",   item.TypeID);
        cmd.Parameters.AddWithValue("@Name", item.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc", (object?)ImportHelper.NullIfEmpty(item.Description) ?? DBNull.Value);
    }

    public async Task<int> EnsureAsync(string name, CancellationToken ct = default)
    {
        name = name.Trim();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var getCmd = new SqlCommand(
            "SELECT [TypeID] FROM [dbo].[AccountTypes] WHERE [Name]=@Name;", conn);
        getCmd.Parameters.AddWithValue("@Name", name);
        var existing = await getCmd.ExecuteScalarAsync(ct);
        if (existing is int id) return id;

        await using var insCmd = new SqlCommand(
            "INSERT INTO [dbo].[AccountTypes]([Name],[Description]) OUTPUT INSERTED.[TypeID] VALUES(@Name,'');", conn);
        insCmd.Parameters.AddWithValue("@Name", name);
        return (int)(await insCmd.ExecuteScalarAsync(ct))!;
    }
}
