using System;
using CompeteDesk.Models.Common;

namespace CompeteDesk.Models.Gamification;

public sealed class UserBadge : IAuditableEntity
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;

    public string BadgeKey { get; set; } = string.Empty;
    public string BadgeName { get; set; } = string.Empty;
    public string? BadgeIcon { get; set; }

    public DateTime EarnedAtUtc { get; set; }

    // Audit
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }
}
