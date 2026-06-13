namespace Portal.Models;

public sealed class PayableLineItem
{
    public int     LineItemID    { get; set; }
    public int     PayableID     { get; set; }
    public int     LineNumber    { get; set; }
    public int?    ItemID        { get; set; }
    public string  Description   { get; set; } = string.Empty;
    public decimal Quantity      { get; set; } = 1;
    public decimal UnitPrice     { get; set; }
    public decimal ExtendedPrice { get; set; }
}
