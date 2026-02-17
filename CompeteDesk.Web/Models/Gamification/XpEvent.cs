using System;
using CompeteDesk.Models.Common;

namespace CompeteDesk.Models.Gamification;

public sealed class XpEvent : IAuditableEntity
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;

    public int Xp { get; set; }
    public string Reason { get; set; } = string.Empty;

    public string? SourceType { get; set; }
    public int? SourceId { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    // Audit
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }
}
