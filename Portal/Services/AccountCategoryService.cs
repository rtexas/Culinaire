using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class AccountCategoryService : NameDescServiceBase<AccountCategory>
{
    public AccountCategoryService(string connectionString) : base(connectionString) { }

    protected override string Table   => "AccountCategories";
    protected override string IdCol   => "CategoryID";
    protected override string OrderBy => "Name";

    protected override AccountCategory Map(SqlDataReader r) => new()
    {
        CategoryID  = r.GetInt32(0),
        Name        = r.GetString(1),
        Description = r.IsDBNull(2) ? string.Empty : r.GetString(2),
        IsActive    = r.GetBoolean(3),
        CreatedAt   = r.GetDateTime(4),
    };

    protected override void BindCreate(SqlCommand cmd, AccountCategory item)
    {
        cmd.Parameters.AddWithValue("@Name", item.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc", (object?)ImportHelper.NullIfEmpty(item.Description) ?? DBNull.Value);
    }

    protected override void BindUpdate(SqlCommand cmd, AccountCategory item)
    {
        cmd.Parameters.AddWithValue("@ID",   item.CategoryID);
        cmd.Parameters.AddWithValue("@Name", item.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc", (object?)ImportHelper.NullIfEmpty(item.Description) ?? DBNull.Value);
    }

    public async Task<int> EnsureAsync(string name, CancellationToken ct = default)
    {
        name = name.Trim();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var getCmd = new SqlCommand(
            "SELECT [CategoryID] FROM [dbo].[AccountCategories] WHERE [Name]=@Name;", conn);
        getCmd.Parameters.AddWithValue("@Name", name);
        var existing = await getCmd.ExecuteScalarAsync(ct);
        if (existing is int id) return id;

        await using var insCmd = new SqlCommand(
            "INSERT INTO [dbo].[AccountCategories]([Name],[Description]) OUTPUT INSERTED.[CategoryID] VALUES(@Name,'');", conn);
        insCmd.Parameters.AddWithValue("@Name", name);
        return (int)(await insCmd.ExecuteScalarAsync(ct))!;
    }
}
