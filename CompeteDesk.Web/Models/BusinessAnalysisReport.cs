using System;
using System.ComponentModel.DataAnnotations;
using CompeteDesk.Models.Common;

namespace CompeteDesk.Models;

/// <summary>
/// AI-generated business analysis for a workspace.
/// Stores SWOT + Porter's Five Forces for the business and key competitors.
/// </summary>
public class BusinessAnalysisReport : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }

    public int WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }

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

    public DateTime? UpdatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedById { get; set; }
}
