namespace Portal.Models;

public sealed class Vendor
{
    public int      VendorID                 { get; set; }
    public string   VendorCode               { get; set; } = string.Empty;
    public string   Name                     { get; set; } = string.Empty;
    public string   Description              { get; set; } = string.Empty;
    public string   Address1                 { get; set; } = string.Empty;
    public string   Address2                 { get; set; } = string.Empty;
    public string   Address3                 { get; set; } = string.Empty;
    public string   City                     { get; set; } = string.Empty;
    public int?     StateRegionID            { get; set; }
    public string   PostalCode               { get; set; } = string.Empty;
    public int?     CountryID                { get; set; }
    public int?     DefaultPayablesAccountID { get; set; }
    public bool     IsActive                 { get; set; } = true;
    public DateTime CreatedAt                { get; set; }

    // Joined display fields — not persisted
    public string StateCode       { get; set; } = string.Empty;
    public string StateName       { get; set; } = string.Empty;
    public string CountryCode     { get; set; } = string.Empty;
    public string CountryName     { get; set; } = string.Empty;
    public string PayablesAccount { get; set; } = string.Empty;
}
