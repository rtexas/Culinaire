namespace Portal.Models;

public sealed class Department
{
    public int      DepartmentID { get; set; }
    public string   Code         { get; set; } = string.Empty;
    public string   Name         { get; set; } = string.Empty;
    public string   Description  { get; set; } = string.Empty;
    public bool     IsActive     { get; set; } = true;
    public DateTime CreatedAt    { get; set; }

    // Location assignments (joined)
    public List<int> LocationIDs { get; set; } = [];
}
