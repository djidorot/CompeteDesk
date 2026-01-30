using System;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.Models;

/// <summary>
/// A repeatable routine tied to a Workspace and (optionally) a Strategy.
/// Frequency is stored as a short string ("Daily" / "Weekly") for SQLite friendliness.
/// </summary>
public class Habit
{
    public int Id { get; set; }

    [Required]
    public int WorkspaceId { get; set; }

    public int? StrategyId { get; set; }

    [Required]
    public string OwnerId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// "Daily" or "Weekly"
    /// </summary>
    [Required, MaxLength(16)]
    public string Frequency { get; set; } = "Daily";

    /// <summary>
    /// Target completions per period. Daily: per day. Weekly: per week.
    /// </summary>
    public int TargetCount { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
