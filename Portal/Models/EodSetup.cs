namespace Portal.Models;

public sealed class EodSetup
{
    public int      SetupID       { get; set; }
    public int      LocationID    { get; set; }
    public int      VersionNumber { get; set; }
    public string   Description   { get; set; } = string.Empty;
    public bool     IsEnabled     { get; set; }
    public DateTime CreatedAt     { get; set; }

    // Joined display
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
}

public sealed class EodSetupColumnDef
{
    public int    SetupColumnID    { get; set; }
    public int    ColumnID         { get; set; }
    public string ColumnName       { get; set; } = string.Empty;
    public string ColumnDesc       { get; set; } = string.Empty;
    public int    DisplayOrder     { get; set; }
    public int    CoaSegmentNumber { get; set; }
    public string SegmentValue     { get; set; } = string.Empty;
}

public sealed class EodSetupRowDef
{
    public int    SetupRowID   { get; set; }
    public int    RowID        { get; set; }
    public string RowName      { get; set; } = string.Empty;
    public string RowDesc      { get; set; } = string.Empty;
    public int    DisplayOrder { get; set; }
    public int    SectionID    { get; set; }
    public string SectionName  { get; set; } = string.Empty;
    public int    Multiplier   { get; set; } = 1;
}

public sealed class EodSetupCell
{
    public int    CellID        { get; set; }
    public int    SetupID       { get; set; }
    public int    RowID         { get; set; }
    public int    ColumnID      { get; set; }
    public int?   AccountID     { get; set; }
    public string AccountString { get; set; } = string.Empty;
}

public sealed class EodSetupLayout
{
    public EodSetup                Setup   { get; set; } = new();
    public List<EodSetupColumnDef> Columns { get; set; } = [];
    public List<EodSetupRowDef>    Rows    { get; set; } = [];
    public List<EodSetupCell>      Cells   { get; set; } = [];

    public EodSetupCell? GetCell(int rowID, int columnID) =>
        Cells.FirstOrDefault(c => c.RowID == rowID && c.ColumnID == columnID);
}
