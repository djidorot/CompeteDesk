using System;
using System.ComponentModel.DataAnnotations;
using CompeteDesk.Models.Common;

namespace CompeteDesk.Models;

/// <summary>
/// Concrete, trackable work that executes a Strategy.
/// Think: "What will we do this week to apply Strategy X?".
/// </summary>
public class ActionItem : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }

    // Optional relationships (kept nullable to avoid forcing a strict workflow in MVP)
    public int? WorkspaceId { get; set; }
    public int? StrategyId { get; set; }

    public Workspace? Workspace { get; set; }
    public Strategy? Strategy { get; set; }

    [Required]
    public string OwnerId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(80)]
    public string? Category { get; set; }

    // Simple workflow.
    // Values suggested: Planned | In Progress | Done | Archived
    [Required]
    [MaxLength(24)]
    public string Status { get; set; } = "Planned";

    public int Priority { get; set; } = 0;

    public DateTime? DueAtUtc { get; set; }

    // Optional provenance.
    [MaxLength(120)]
    public string? SourceBook { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedById { get; set; }
}
