namespace Portal.Models;

public sealed class ChartOfAccount
{
    public int      AccountID          { get; set; }
    public string   AccountName        { get; set; } = string.Empty;
    public string   AccountDescription { get; set; } = string.Empty;
    public int?     CategoryID         { get; set; }
    public int?     TypeID             { get; set; }
    public bool     IsActive           { get; set; } = true;
    public DateTime CreatedAt          { get; set; }

    // Joined from lookup tables — not persisted
    public string CategoryName { get; set; } = string.Empty;
    public string TypeName     { get; set; } = string.Empty;
}
