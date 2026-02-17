using System;
using CompeteDesk.Models.Common;

namespace CompeteDesk.Models;

public sealed class StudyPlan : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }

    public int? WorkspaceId { get; set; }
    public string OwnerId { get; set; } = string.Empty;

    public string Title { get; set; } = "Weekly Study Plan";

    /// <summary>
    /// Start of the plan week (UTC date).
    /// </summary>
    public DateTime WeekStartUtc { get; set; }

    /// <summary>
    /// Desired weekly minutes.
    /// </summary>
    public int WeeklyMinutesTarget { get; set; }

    /// <summary>
    /// Optional AI roadmap/plan notes stored as JSON.
    /// </summary>
    public string? AiRoadmapJson { get; set; }

    // Audit
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedById { get; set; }
}
