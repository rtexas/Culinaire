namespace Portal.Models;

public sealed class AccountCategory
{
    public int      CategoryID  { get; set; }
    public string   Name        { get; set; } = string.Empty;
    public string   Description { get; set; } = string.Empty;
    public bool     IsActive    { get; set; } = true;
    public DateTime CreatedAt   { get; set; }
}
