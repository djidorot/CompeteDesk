using System;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.Models;

/// <summary>
/// AI-generated business analysis for a workspace.
/// Stores SWOT + Porter's Five Forces for the business and key competitors.
/// </summary>
public class BusinessAnalysisReport
{
    public int Id { get; set; }

    public int WorkspaceId { get; set; }

    [Required]
    public string OwnerId { get; set; } = string.Empty;

    [StringLength(120)]
    public string BusinessType { get; set; } = string.Empty;

    [StringLength(80)]
    public string Country { get; set; } = string.Empty;

    /// <summary>
    /// Full JSON returned by AI (response_format: json_object).
    /// </summary>
    public string AiInsightsJson { get; set; } = "{}";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
