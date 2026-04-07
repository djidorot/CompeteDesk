using System;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.Models;

public class StrategyComment
{
    public int Id { get; set; }

    public int StrategyId { get; set; }
    public Strategy? Strategy { get; set; }

    public int? WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }

    [Required]
    public string OwnerId { get; set; } = string.Empty;

    [Required]
    public string AuthorUserId { get; set; } = string.Empty;

    [StringLength(256)]
    public string? AuthorEmail { get; set; }

    [Required]
    [StringLength(2000)]
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
