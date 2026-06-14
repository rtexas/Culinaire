namespace Portal.Models;

public sealed class JobRole
{
    public int      JobRoleID   { get; set; }
    public string   ExternalID  { get; set; } = string.Empty;
    public string   Name        { get; set; } = string.Empty;
    public string   Description { get; set; } = string.Empty;
    public bool     IsExempt    { get; set; }
    public bool     IsActive    { get; set; } = true;
    public DateTime CreatedAt   { get; set; }
}
