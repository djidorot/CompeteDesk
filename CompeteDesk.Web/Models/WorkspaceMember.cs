using System;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.Models;

public class WorkspaceMember
{
    public int Id { get; set; }

    public int WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [StringLength(256)]
    public string? UserEmail { get; set; }

    [Required]
    [StringLength(24)]
    public string Role { get; set; } = "Viewer";

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    public string? InvitedByUserId { get; set; }
}
