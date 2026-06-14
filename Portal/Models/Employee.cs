namespace Portal.Models;

public sealed class Employee
{
    public int      EmployeeID  { get; set; }
    public string   ExternalID  { get; set; } = string.Empty;
    public string   Name        { get; set; } = string.Empty;
    public string   Description { get; set; } = string.Empty;
    public bool     IsActive    { get; set; } = true;
    public DateTime CreatedAt   { get; set; }

    // Location assignments (joined)
    public List<int> LocationIDs { get; set; } = [];
}
