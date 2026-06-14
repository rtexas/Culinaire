namespace Portal.Models;

public sealed class ChartOfAccount
{
    public int      AccountID          { get; set; }
    public string   AccountName        { get; set; } = string.Empty;
    public string   AccountDescription { get; set; } = string.Empty;
    public int?     CategoryID         { get; set; }
    public int?     TypeID             { get; set; }
    public bool     IsActive           { get; set; } = true;

    // Segment-based G/L account string (used for EOD Sales account filtering)
    public string   FullAccountString  { get; set; } = string.Empty;
    public string   Seg1Value          { get; set; } = string.Empty;
    public string   Seg2Value          { get; set; } = string.Empty;
    public string   Seg3Value          { get; set; } = string.Empty;
    public string   Seg4Value          { get; set; } = string.Empty;
    public string   Seg5Value          { get; set; } = string.Empty;
    public string   Seg6Value          { get; set; } = string.Empty;

    public DateTime CreatedAt          { get; set; }

    // Joined from lookup tables — not persisted
    public string CategoryName { get; set; } = string.Empty;
    public string TypeName     { get; set; } = string.Empty;

    public string GetSegValue(int segNum) => segNum switch
    {
        1 => Seg1Value, 2 => Seg2Value, 3 => Seg3Value,
        4 => Seg4Value, 5 => Seg5Value, 6 => Seg6Value,
        _ => string.Empty,
    };
}
