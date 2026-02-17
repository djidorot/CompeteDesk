using System;
using CompeteDesk.Models.Common;

namespace CompeteDesk.Models;

public sealed class StudyPlanItem : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public int StudyPlanId { get; set; }
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>
    /// UTC date (no time) the item is scheduled on.
    /// </summary>
    public DateTime ScheduledOnUtc { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }

    /// <summary>
    /// Planned duration.
    /// </summary>
    public int Minutes { get; set; }

    /// <summary>
    /// Habit / Action / Reading / Review
    /// </summary>
    public string ItemType { get; set; } = "Task";

    public int? SourceEntityId { get; set; }
    public string? SourceEntityType { get; set; }

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
