using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class ShippingMethodService : NameDescServiceBase<ShippingMethod>
{
    public ShippingMethodService(string connectionString) : base(connectionString) { }

    protected override string Table   => "ShippingMethods";
    protected override string IdCol   => "ShippingMethodID";
    protected override string OrderBy => "Name";

    protected override ShippingMethod Map(SqlDataReader r) => new()
    {
        ShippingMethodID = r.GetInt32(0),
        Name             = r.GetString(1),
        Description      = r.IsDBNull(2) ? string.Empty : r.GetString(2),
        IsActive         = r.GetBoolean(3),
    };

    protected override void BindCreate(SqlCommand cmd, ShippingMethod item)
    {
        cmd.Parameters.AddWithValue("@Name", item.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc", (object?)ImportHelper.NullIfEmpty(item.Description) ?? DBNull.Value);
    }

    protected override void BindUpdate(SqlCommand cmd, ShippingMethod item)
    {
        cmd.Parameters.AddWithValue("@ID",   item.ShippingMethodID);
        cmd.Parameters.AddWithValue("@Name", item.Name.Trim());
        cmd.Parameters.AddWithValue("@Desc", (object?)ImportHelper.NullIfEmpty(item.Description) ?? DBNull.Value);
    }
}
