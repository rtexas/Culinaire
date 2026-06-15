namespace Portal.Models;

public sealed class EodSalesEntry
{
    public int       EntryID     { get; set; }
    public DateTime  EntryDate   { get; set; }
    public int       LocationID  { get; set; }
    public int       SetupID     { get; set; }
    public bool      IsSubmitted { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public bool      IsVoided    { get; set; }
    public DateTime? VoidedAt    { get; set; }
    public DateTime  CreatedAt   { get; set; }
    public DateTime  UpdatedAt   { get; set; }

    public string Status => IsVoided ? "Voided" : IsSubmitted ? "Submitted" : "Saved";
}

public sealed class EodSalesValue
{
    public int     ValueID  { get; set; }
    public int     EntryID  { get; set; }
    public int     RowID    { get; set; }
    public int     ColumnID { get; set; }
    public decimal Amount   { get; set; }
}

public sealed class EodSalesGridSection
{
    public int                  SectionID   { get; set; }
    public string               SectionName { get; set; } = string.Empty;
    public int                  Multiplier    { get; set; }
    public bool                 UseInEodSales { get; set; } = true;
    public List<EodSetupRowDef> Rows          { get; set; } = [];
}

public sealed class EodSalesGrid
{
    public EodSalesEntry             Entry    { get; set; } = new();
    public EodSetupLayout            Layout   { get; set; } = new();
    public List<EodSalesGridSection> Sections { get; set; } = [];
    /// <summary>Loaded cell amounts keyed by (RowID, ColumnID).</summary>
    public Dictionary<(int RowID, int ColumnID), decimal> Values { get; set; } = [];

    public decimal GetValue(int rowID, int colID) =>
        Values.TryGetValue((rowID, colID), out var v) ? v : 0m;
}
