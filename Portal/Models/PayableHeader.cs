namespace Portal.Models;

public sealed class PayableHeader
{
    public int       PayableID          { get; set; }
    public int       VendorID           { get; set; }
    public string    VendorName         { get; set; } = string.Empty;
    public string    InvoiceNumber      { get; set; } = string.Empty;
    public DateTime  InvoiceDate        { get; set; } = DateTime.Today;
    public DateTime? DueDate            { get; set; }
    public int?      ShippingMethodID   { get; set; }
    public string    ShippingMethodName { get; set; } = string.Empty;
    public decimal   ShippingCharge     { get; set; }
    public decimal   TaxAmount          { get; set; }
    public string    Notes              { get; set; } = string.Empty;
    public int?      LocationID         { get; set; }
    public string    Status             { get; set; } = "Saved";
    public decimal   Subtotal           { get; set; }
    public decimal   Total              => Subtotal + ShippingCharge + TaxAmount;
    public DateTime  CreatedAt          { get; set; }
    public DateTime  UpdatedAt          { get; set; }
}
