using System;
using CompeteDesk.Models.Common;

namespace CompeteDesk.Models.Gamification;

public sealed class UserGamificationProfile : IAuditableEntity
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;

    public int TotalXp { get; set; }
    public int Level { get; set; } = 1;
    public string Rank { get; set; } = "Rookie";

    public int CurrentStreakDays { get; set; }
    public int LongestStreakDays { get; set; }
    public DateTime? LastActivityUtc { get; set; }

    // Audit
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }
}
