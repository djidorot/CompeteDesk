using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Models.Gamification;

namespace CompeteDesk.Services.Gamification;

/// <summary>
/// Simple XP/Badge/Rank system.
/// - XP is earned from completions/check-ins.
/// - Level is derived from XP.
/// - Rank is derived from Level.
/// This is intentionally lightweight and deterministic; AI can layer on later.
/// </summary>
public sealed class GamificationService
{
    private readonly ApplicationDbContext _db;

    public GamificationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public static int ComputeLevel(int totalXp)
    {
        // 0-99 = L1, 100-199 = L2, ...
        return Math.Max(1, (totalXp / 100) + 1);
    }

    public static string ComputeRank(int level)
    {
        return level switch
        {
            <= 2 => "Rookie",
            <= 4 => "Apprentice",
            <= 7 => "Operator",
            <= 10 => "Strategist",
            <= 15 => "Tactician",
            _ => "Legend"
        };
    }

    public async Task<UserGamificationProfile> GetOrCreateAsync(string ownerId, CancellationToken ct)
    {
        var profile = await _db.UserGamificationProfiles.FirstOrDefaultAsync(x => x.OwnerId == ownerId, ct);
        if (profile != null) return profile;

        profile = new UserGamificationProfile
        {
            OwnerId = ownerId,
            TotalXp = 0,
            Level = 1,
            Rank = "Rookie",
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.UserGamificationProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task AwardXpAsync(
        string ownerId,
        int xp,
        string reason,
        string? sourceType,
        int? sourceId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || xp == 0) return;
        reason = string.IsNullOrWhiteSpace(reason) ? "Progress" : reason.Trim();

        var profile = await GetOrCreateAsync(ownerId, ct);

        // Update streak based on last activity date.
        var today = DateTime.UtcNow.Date;
        var last = profile.LastActivityUtc?.Date;

        if (!last.HasValue)
        {
            profile.CurrentStreakDays = 1;
            profile.LongestStreakDays = Math.Max(profile.LongestStreakDays, profile.CurrentStreakDays);
        }
        else if (last.Value == today)
        {
            // same day, do not change streak
        }
        else if (last.Value == today.AddDays(-1))
        {
            profile.CurrentStreakDays += 1;
            profile.LongestStreakDays = Math.Max(profile.LongestStreakDays, profile.CurrentStreakDays);
        }
        else
        {
            profile.CurrentStreakDays = 1;
        }

        profile.LastActivityUtc = DateTime.UtcNow;

        profile.TotalXp += Math.Max(0, xp);
        profile.Level = ComputeLevel(profile.TotalXp);
        profile.Rank = ComputeRank(profile.Level);
        profile.UpdatedAtUtc = DateTime.UtcNow;

        _db.XpEvents.Add(new XpEvent
        {
            OwnerId = ownerId,
            Xp = xp,
            Reason = reason.Length > 160 ? reason.Substring(0, 160) : reason,
            SourceType = sourceType,
            SourceId = sourceId,
            OccurredAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);

        // Badge checks (non-blocking, best-effort).
        try
        {
            await EvaluateBadgesAsync(ownerId, profile, ct);
        }
        catch
        {
            // Never block main flows.
        }
    }

    private async Task EvaluateBadgesAsync(string ownerId, UserGamificationProfile profile, CancellationToken ct)
    {
        var defs = await _db.BadgeDefinitions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        if (defs.Count == 0) return;

        var earnedKeys = await _db.UserBadges.AsNoTracking()
            .Where(x => x.OwnerId == ownerId)
            .Select(x => x.BadgeKey)
            .ToListAsync(ct);
        var earned = new HashSet<string>(earnedKeys, StringComparer.OrdinalIgnoreCase);

        var toGrant = new List<BadgeDefinition>();

        // First habit check-in
        if (!earned.Contains("first_checkin"))
        {
            var anyCheckin = await _db.HabitCheckins.AsNoTracking().AnyAsync(x => x.OwnerId == ownerId, ct);
            if (anyCheckin) toGrant.Add(defs.FirstOrDefault(d => d.Key == "first_checkin")!);
        }

        // First action completed
        if (!earned.Contains("first_action_done"))
        {
            var anyDone = await _db.Actions.AsNoTracking()
                .AnyAsync(x => x.OwnerId == ownerId && x.Status == "Completed", ct);
            if (anyDone) toGrant.Add(defs.FirstOrDefault(d => d.Key == "first_action_done")!);
        }

        // Streak badges
        if (profile.CurrentStreakDays >= 7 && !earned.Contains("week_streak"))
            toGrant.Add(defs.FirstOrDefault(d => d.Key == "week_streak")!);

        if (profile.CurrentStreakDays >= 30 && !earned.Contains("month_streak"))
            toGrant.Add(defs.FirstOrDefault(d => d.Key == "month_streak")!);

        foreach (var b in toGrant.Where(x => x != null).DistinctBy(x => x.Key))
        {
            if (earned.Contains(b.Key)) continue;

            _db.UserBadges.Add(new UserBadge
            {
                OwnerId = ownerId,
                BadgeKey = b.Key,
                BadgeName = b.Name,
                BadgeIcon = b.Icon,
                EarnedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            });

            if (b.XpReward > 0)
            {
                // Prevent badge XP from looping into more badge checks.
                profile.TotalXp += b.XpReward;
                profile.Level = ComputeLevel(profile.TotalXp);
                profile.Rank = ComputeRank(profile.Level);
                profile.UpdatedAtUtc = DateTime.UtcNow;

                _db.XpEvents.Add(new XpEvent
                {
                    OwnerId = ownerId,
                    Xp = b.XpReward,
                    Reason = $"Badge: {b.Name}",
                    SourceType = "Badge",
                    SourceId = b.Id,
                    OccurredAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
