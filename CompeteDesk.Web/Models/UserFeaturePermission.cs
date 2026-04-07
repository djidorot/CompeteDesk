namespace CompeteDesk.Models;

public class UserFeaturePermission
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string PermissionKey { get; set; } = string.Empty;
    public bool IsGranted { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
