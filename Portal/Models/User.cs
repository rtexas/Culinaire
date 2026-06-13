namespace Portal.Models;

public sealed class User
{
    public int      UserID      { get; set; }
    public string   Username    { get; set; } = string.Empty;
    public string   PasswordHash{ get; set; } = string.Empty;
    public string   PasswordSalt{ get; set; } = string.Empty;
    public string   FullName    { get; set; } = string.Empty;
    public string?  Email       { get; set; }
    public string   RoleType    { get; set; } = "User";   // Administrator | User | Viewer
    public bool     IsActive    { get; set; } = true;
    public DateTime CreatedAt   { get; set; }
    public DateTime?LastLoginAt { get; set; }

    // Optional address
    public string?  Address1      { get; set; }
    public string?  Address2      { get; set; }
    public string?  Address3      { get; set; }
    public string?  City          { get; set; }
    public int?     StateRegionID { get; set; }
    public string?  PostalCode    { get; set; }
    public int?     CountryID     { get; set; }

    // Joined display fields
    public string   StateCode   { get; set; } = string.Empty;
    public string   StateName   { get; set; } = string.Empty;
    public string   CountryCode { get; set; } = string.Empty;
    public string   CountryName { get; set; } = string.Empty;
}
