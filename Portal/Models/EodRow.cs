namespace Portal.Models;

public sealed class EodRow
{
    public int      RowID       { get; set; }
    public string   Name        { get; set; } = string.Empty;
    public string   Description { get; set; } = string.Empty;
    public int      SectionID   { get; set; }
    public DateTime CreatedAt   { get; set; }

    // Joined display — not persisted
    public string SectionName { get; set; } = string.Empty;
}
