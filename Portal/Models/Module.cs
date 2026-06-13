namespace Portal.Models;

public sealed class Module
{
    public int    ModuleID    { get; set; }
    public string ModuleName  { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RouteUrl    { get; set; } = string.Empty;
    public string? IconClass  { get; set; }
    public int    SortOrder   { get; set; }
    public bool   IsActive    { get; set; } = true;
}
