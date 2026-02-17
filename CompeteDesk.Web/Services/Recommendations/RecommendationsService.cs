using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Services.OpenAI;

namespace CompeteDesk.Services.Recommendations;

public sealed class RecommendationsService
{
    private readonly ApplicationDbContext _db;
    private readonly OpenAiChatClient _ai;

    public RecommendationsService(ApplicationDbContext db, OpenAiChatClient ai)
    {
        _db = db;
        _ai = ai;
    }

    public bool IsAiConfigured => _ai.IsConfigured;

    public sealed record Recommendation(string Title, string Message, string Type);

    public async Task<IReadOnlyList<Recommendation>> GetAsync(string ownerId, int? workspaceId, CancellationToken ct)
    {
        var recs = new List<Recommendation>();
        var now = DateTime.UtcNow;
        var from = now.Date.AddDays(-14);

        // Actions: overdue or stalled.
        var actions = await _db.Actions.AsNoTracking()
            .Where(a => a.OwnerId == ownerId && a.Status != "Completed" && (!workspaceId.HasValue || a.WorkspaceId == workspaceId))
            .OrderBy(a => a.DueAtUtc ?? DateTime.MaxValue)
            .ThenBy(a => a.Priority)
            .Take(25)
            .ToListAsync(ct);

        var overdue = actions.Where(a => a.DueAtUtc.HasValue && a.DueAtUtc.Value.Date < now.Date).Take(3).ToList();
        if (overdue.Count > 0)
        {
            recs.Add(new Recommendation(
                "You struggle with follow-through",
                $"You have {overdue.Count} overdue action(s). Pick ONE: \"{overdue[0].Title}\" and finish a 15-minute slice today.",
                "Focus"));
        }
        else if (actions.Count > 10)
        {
            recs.Add(new Recommendation(
                "Too many open actions",
                "You have a big action backlog. Park 3 low-priority items and protect 2 high-impact actions this week.",
                "Focus"));
        }

        // Habits: low activity.
        var habits = await _db.Habits.AsNoTracking()
            .Where(h => h.OwnerId == ownerId && h.IsActive && (!workspaceId.HasValue || h.WorkspaceId == workspaceId))
            .OrderBy(h => h.Title)
            .Take(30)
            .ToListAsync(ct);

        if (habits.Count > 0)
        {
            var habitIds = habits.Select(h => h.Id).ToArray();
            var checkins = await _db.HabitCheckins.AsNoTracking()
                .Where(c => c.OwnerId == ownerId && habitIds.Contains(c.HabitId) && c.OccurredOnUtc >= from)
                .GroupBy(c => c.HabitId)
                .Select(g => new { HabitId = g.Key, Total = g.Sum(x => x.Count) })
                .ToListAsync(ct);

            var byId = checkins.ToDictionary(x => x.HabitId, x => x.Total);
            var weakest = habits
                .Select(h => new { Habit = h, Total = byId.TryGetValue(h.Id, out var t) ? t : 0 })
                .OrderBy(x => x.Total)
                .FirstOrDefault();

            if (weakest != null && weakest.Total == 0)
            {
                recs.Add(new Recommendation(
                    $"You struggle with consistency",
                    $"No activity logged for \"{weakest.Habit.Title}\" in the last 14 days → reduce friction: set a 2-minute minimum and check in daily.",
                    "Habit"));
            }
        }

        // Resource suggestions (book-driven)
        var topBooks = await _db.Actions.AsNoTracking()
            .Where(a => a.OwnerId == ownerId && a.SourceBook != null && a.SourceBook != "" && (!workspaceId.HasValue || a.WorkspaceId == workspaceId))
            .GroupBy(a => a.SourceBook)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key!)
            .Take(3)
            .ToListAsync(ct);

        if (topBooks.Count > 0)
        {
            recs.Add(new Recommendation(
                "Resource suggestion",
                $"Double down on resources already tied to your work: {string.Join(", ", topBooks)}.",
                "Resource"));
        }

        // Optional AI layer for nicer phrasing and next steps.
        var ai = await TryAiRefineAsync(ownerId, workspaceId, recs, ct);
        return ai ?? recs;
    }

    private async Task<IReadOnlyList<Recommendation>?> TryAiRefineAsync(
        string ownerId,
        int? workspaceId,
        List<Recommendation> current,
        CancellationToken ct)
    {
        if (!_ai.IsConfigured || current.Count == 0) return null;

        var payload = new
        {
            intent = "smart_recommendations",
            nowUtc = DateTime.UtcNow,
            workspaceId,
            recommendations = current.Select(r => new { r.Title, r.Message, r.Type })
        };

        var systemPrompt =
            "You are a strategic coach. Output STRICT JSON only. " +
            "Rewrite recommendations to be clear, specific, and motivating. Add one 'nextAction' per item. " +
            "Schema: {\"items\":[{\"title\":string,\"message\":string,\"type\":string,\"nextAction\":string}]}";

        var input = JsonSerializer.Serialize(payload);
        try
        {
            var json = await _ai.CreateJsonInsightsAsync(systemPrompt, input, ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return null;

            var result = new List<Recommendation>();
            foreach (var it in items.EnumerateArray().Take(8))
            {
                var title = it.TryGetProperty("title", out var t) ? t.GetString() : null;
                var msg = it.TryGetProperty("message", out var m) ? m.GetString() : null;
                var type = it.TryGetProperty("type", out var ty) ? ty.GetString() : null;
                var next = it.TryGetProperty("nextAction", out var na) ? na.GetString() : null;

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(msg)) continue;
                var merged = string.IsNullOrWhiteSpace(next) ? msg! : (msg + " Next: " + next);
                result.Add(new Recommendation(title!, merged, string.IsNullOrWhiteSpace(type) ? "Focus" : type!));
            }

            return result.Count > 0 ? result : null;
        }
        catch
        {
            return null;
        }
    }
}
