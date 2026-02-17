using System;
using CompeteDesk.Models.Common;

namespace CompeteDesk.Models.Gamification;

public sealed class BadgeDefinition : IAuditableEntity
{
    public int Id { get; set; }

    public string Key { get; set; } = string.Empty; // e.g. first_checkin
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// Optional icon name (Bootstrap icon / emoji / svg key).
    /// </summary>
    public string? Icon { get; set; }

    public int XpReward { get; set; }
    public bool IsActive { get; set; } = true;

    // Audit
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }
}
