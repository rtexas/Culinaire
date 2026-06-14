namespace Portal.Models;

public sealed class EodColumn
{
    public int      ColumnID         { get; set; }
    public string   Name             { get; set; } = string.Empty;
    public string   Description      { get; set; } = string.Empty;
    /// <summary>CoA segment position this department column maps to. 0 = not linked.</summary>
    public int      CoaSegmentNumber { get; set; }
    /// <summary>The segment value at CoaSegmentNumber position used to filter ChartOfAccounts (e.g. "KITCH", "BAR").</summary>
    public string   SegmentValue     { get; set; } = string.Empty;
    public DateTime CreatedAt        { get; set; }

    // Joined display — not persisted
    public string CoaSegmentDescription { get; set; } = string.Empty;
}
