namespace Portal.Models;

public sealed class Location
{
    public int      LocationID    { get; set; }
    public string   Code          { get; set; } = string.Empty;
    public string   Name          { get; set; } = string.Empty;
    public string   Description   { get; set; } = string.Empty;
    /// <summary>Which CoA segment position this location occupies. 0 = not linked.</summary>
    public int      SegmentNumber { get; set; }
    public bool     IsActive      { get; set; } = true;
    public DateTime CreatedAt     { get; set; }
    // Address fields (added in Addendum_Checks.sql)
    public string   Address1      { get; set; } = string.Empty;
    public string   Address2      { get; set; } = string.Empty;
    public string   City          { get; set; } = string.Empty;
    public string   State         { get; set; } = string.Empty;
    public string   Zip           { get; set; } = string.Empty;
}
