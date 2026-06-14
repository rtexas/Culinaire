namespace Portal.Models;

public sealed class CheckSetupVendor
{
    public int    CheckSetupVendorID { get; set; }
    public int    LocationID         { get; set; }
    public int    VendorID           { get; set; }
    public bool   IsActive           { get; set; } = true;
    // Joined
    public string VendorCode         { get; set; } = string.Empty;
    public string VendorName         { get; set; } = string.Empty;
}

public sealed class CheckSetupAccount
{
    public int    CheckSetupAccountID { get; set; }
    public int    LocationID          { get; set; }
    public int    AccountID           { get; set; }
    public bool   IsActive            { get; set; } = true;
    // Joined
    public string AccountName         { get; set; } = string.Empty;
    public string FullAccountString   { get; set; } = string.Empty;
}
