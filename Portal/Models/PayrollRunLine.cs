namespace Portal.Models;

public sealed class PayrollRunLine
{
    public int     LineID       { get; set; }
    public int     RunID        { get; set; }
    public int     EmployeeID   { get; set; }
    public string  EmployeeName { get; set; } = string.Empty;
    public int     JobRoleID    { get; set; }
    public string  JobRoleName  { get; set; } = string.Empty;
    public string  PayType      { get; set; } = "Hourly";   // Hourly | Exempt
    public decimal Quantity     { get; set; } = 1m;
    public decimal PayRate      { get; set; }
    public decimal TotalAmount  { get; set; }
    public int     SortOrder    { get; set; }
}
