namespace Portal.Models;

public sealed class UserModulePermission
{
    public int    PermissionID    { get; set; }
    public int    UserID          { get; set; }
    public int    ModuleID        { get; set; }
    public string PermissionLevel { get; set; } = "None"; // None | Read | ReadWrite

    // Joined display fields (not persisted)
    public string ModuleDisplayName { get; set; } = string.Empty;
    public string Username          { get; set; } = string.Empty;
}
