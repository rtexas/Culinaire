namespace Portal.Models;

public sealed class ImportResult
{
    public int          AccountsCreated    { get; set; }
    public int          AccountsUpdated    { get; set; }
    public int          CategoriesCreated  { get; set; }
    public int          TypesCreated       { get; set; }
    public int          RowsSkipped        { get; set; }
    public List<string> Errors             { get; set; } = [];

    public bool HasErrors => Errors.Count > 0;
    public int  TotalRows => AccountsCreated + AccountsUpdated + RowsSkipped;
}
