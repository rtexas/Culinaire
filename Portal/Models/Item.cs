namespace Portal.Models;

public sealed class Item
{
    public int      ItemID          { get; set; }
    public string   ItemCode        { get; set; } = string.Empty;
    public string   ItemName        { get; set; } = string.Empty;
    public string   ItemDescription { get; set; } = string.Empty;
    public decimal? TypicalPrice    { get; set; }
    public bool     IsActive        { get; set; } = true;
    public DateTime CreatedAt       { get; set; }
}
