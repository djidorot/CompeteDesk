using System.Collections.Generic;

namespace CompeteDesk.ViewModels.Admin;

public class AdminUsersViewModel
{
    public List<AdminUserItem> Users { get; set; } = new();
}

public class AdminUserItem
{
    public string Id { get; set; } = "";
    public string? Email { get; set; }
    public string? UserName { get; set; }
    public string Role { get; set; } = "User";
}
