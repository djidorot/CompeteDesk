using System;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.Models;

/// <summary>
/// "War Room" intelligence entry: competitor moves, market signals, internal observations,
/// lessons learned, and other inputs that inform plans.
/// </summary>
public class WarIntel
{
    public int Id { get; set; }

    // Optional link to a workspace (MVP keeps it nullable)
    public int? WorkspaceId { get; set; }

    [Required]
    public string OwnerId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Who/what this intel is about (competitor, customer segment, market, regulator, internal, etc.)
    /// </summary>
    [MaxLength(120)]
    public string? Subject { get; set; }

    /// <summary>
    /// Main observation / signal.
    /// </summary>
    [MaxLength(2000)]
    public string? Signal { get; set; }

    /// <summary>
    /// Where did this come from? (link, meeting, report, social post, interview, etc.)
    /// </summary>
    [MaxLength(300)]
    public string? Source { get; set; }

    /// <summary>
    /// Confidence score 1-5 (low to high). Stored as int for easy sorting.
    /// </summary>
    [Range(1, 5)]
    public int Confidence { get; set; } = 3;

    /// <summary>
    /// Comma-separated tags for MVP. (Later: normalize)
    /// </summary>
    [MaxLength(200)]
    public string? Tags { get; set; }

    /// <summary>
    /// Notes / interpretation / implications.
    /// </summary>
    [MaxLength(4000)]
    public string? Notes { get; set; }

    /// <summary>
    /// When the signal happened or was observed (optional).
    /// </summary>
    public DateTime? ObservedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
