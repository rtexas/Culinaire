namespace Portal.Models;

public sealed class ShippingMethod
{
    public int    ShippingMethodID { get; set; }
    public string Name             { get; set; } = string.Empty;
    public string Description      { get; set; } = string.Empty;
    public bool   IsActive         { get; set; } = true;
}
