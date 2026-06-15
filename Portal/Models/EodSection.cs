namespace Portal.Models;

public sealed class EodSection
{
    public int      SectionID   { get; set; }
    public string   Name        { get; set; } = string.Empty;
    public string   Description { get; set; } = string.Empty;
    /// <summary>-1 = subtract, 0 = neutral/ignored, 1 = add</summary>
    public int      Multiplier      { get; set; } = 1;
    /// <summary>When true the section total is included in the EOD Grand Total; when false rows use the Multiplier but are excluded from the Grand Total.</summary>
    public bool     UseInEodSales   { get; set; } = true;
    /// <summary>When true the section total is plotted on the Dashboard EOD Sales line graph.</summary>
    public bool     UseInEodGraph   { get; set; } = false;
    public DateTime CreatedAt       { get; set; }
}
