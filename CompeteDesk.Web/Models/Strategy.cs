using System;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.Models;

/// <summary>
/// A Strategy is a reusable “play” you can apply to a Workspace.
/// In CompeteDesk, you can create strategies inspired by books like
/// Capture strategies and adapt them to your business context.
/// </summary>
public class Strategy
{
    public int Id { get; set; }

    /// <summary>
    /// Optional Workspace this strategy is attached to.
    /// Keep nullable so you can draft strategies before choosing a workspace.
    /// </summary>
    public int? WorkspaceId { get; set; }

    public Workspace? Workspace { get; set; }

    /// <summary>
    /// IdentityUser.Id of the owner.
    /// </summary>
    [Required]
    public string OwnerId { get; set; } = string.Empty;

    [Required]
    [StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [StringLength(120)]
    public string? SourceBook { get; set; }

    [StringLength(300)]
    public string? CorePrinciple { get; set; }

    [StringLength(2000)]
    public string? Summary { get; set; }

    /// <summary>
    /// Free-form category (e.g., Self-Directed, Defensive, Offensive, Unconventional).
    /// </summary>
    [StringLength(80)]
    public string? Category { get; set; }

    /// <summary>
    /// High-level strategy type used for quick “battle readiness” awareness in the UI.
    /// Values: Offensive | Defensive | Growth | Stability
    /// </summary>
    [StringLength(24)]
    public string StrategyType { get; set; } = "Growth";

    /// <summary>
    /// "Active" | "Archived"
    /// </summary>
    [Required]
    [StringLength(24)]
    public string Status { get; set; } = "Active";

    /// <summary>
    /// Higher = more important.
    /// </summary>
    public int Priority { get; set; } = 0;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    // ------------------------------------------------------------
    // AI (Competitive Playbook)
    // ------------------------------------------------------------
    /// <summary>
    /// Raw JSON returned by the AI Playbook generator.
    /// Stored so users can revisit / iterate without re-running AI.
    /// </summary>
    public string? AiInsightsJson { get; set; }

    /// <summary>
    /// Short human-readable summary of the last AI run.
    /// </summary>
    public string? AiSummary { get; set; }

    /// <summary>
    /// Timestamp of the last AI generation for this strategy.
    /// </summary>
    public DateTime? AiUpdatedAtUtc { get; set; }
}
