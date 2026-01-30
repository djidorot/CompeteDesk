using System;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.Models;

/// <summary>
/// "War Room" plan: an actionable operational plan informed by intelligence.
/// MVP keeps this as a single record with concise fields.
/// </summary>
public class WarPlan
{
    public int Id { get; set; }

    public int? WorkspaceId { get; set; }

    [Required]
    public string OwnerId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Objective { get; set; }

    /// <summary>
    /// The key idea / approach (often derived from a Strategy).
    /// </summary>
    [MaxLength(2000)]
    public string? Approach { get; set; }

    [MaxLength(4000)]
    public string? Assumptions { get; set; }

    [MaxLength(4000)]
    public string? Risks { get; set; }

    [MaxLength(4000)]
    public string? Contingencies { get; set; }

    // Suggested values: Draft | Active | Completed | Archived
    [Required]
    [MaxLength(24)]
    public string Status { get; set; } = "Draft";

    public DateTime? StartAtUtc { get; set; }
    public DateTime? EndAtUtc { get; set; }

    [MaxLength(120)]
    public string? SourceBook { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
