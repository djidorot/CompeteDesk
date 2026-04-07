using System;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.Models;

public class WorkspaceInvite
{
    public int Id { get; set; }

    public int WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }

    [Required]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(24)]
    public string Role { get; set; } = "Viewer";

    [Required]
    [StringLength(24)]
    public string Status { get; set; } = "Pending";

    [Required]
    public string InvitedByUserId { get; set; } = string.Empty;

    [StringLength(256)]
    public string? InvitedByEmail { get; set; }

    public string? AcceptedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAtUtc { get; set; }
}
