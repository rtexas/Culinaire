namespace Portal.Models;

public sealed class EodSection
{
    public int      SectionID   { get; set; }
    public string   Name        { get; set; } = string.Empty;
    public string   Description { get; set; } = string.Empty;
    /// <summary>-1 = subtract, 0 = neutral/ignored, 1 = add</summary>
    public int      Multiplier  { get; set; } = 1;
    public DateTime CreatedAt   { get; set; }
}
