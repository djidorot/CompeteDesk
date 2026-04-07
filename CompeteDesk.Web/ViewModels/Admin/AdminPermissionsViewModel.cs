namespace CompeteDesk.ViewModels.Admin;

public class AdminPermissionsViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? UserName { get; set; }
    public string Role { get; set; } = "User";
    public List<AdminPermissionGroup> Groups { get; set; } = new();
}

public class AdminPermissionGroup
{
    public string Name { get; set; } = string.Empty;
    public List<AdminPermissionItem> Permissions { get; set; } = new();
}

public class AdminPermissionItem
{
    public string Key { get; set; } = string.Empty;
    public string FeatureName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsGranted { get; set; }
}
