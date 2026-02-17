using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Models;
using CompeteDesk.Services.OpenAI;

namespace CompeteDesk.Services.StudyPlanner;

public sealed class StudyPlannerService
{
    private readonly ApplicationDbContext _db;
    private readonly OpenAiChatClient _ai;

    public StudyPlannerService(ApplicationDbContext db, OpenAiChatClient ai)
    {
        _db = db;
        _ai = ai;
    }

    public bool IsAiConfigured => _ai.IsConfigured;

    public sealed class GenerateRequest
    {
        public int? WorkspaceId { get; set; }
        public DateTime? WeekStartUtc { get; set; }
        public int WeeklyMinutesTarget { get; set; } = 300;
        public string? Goal { get; set; }
    }

    public async Task<StudyPlan> GenerateWeeklyPlanAsync(string ownerId, GenerateRequest req, CancellationToken ct)
    {
        req ??= new GenerateRequest();
        var weekStart = (req.WeekStartUtc?.Date ?? DateTime.UtcNow.Date);

        // Normalize to Monday.
        while (weekStart.DayOfWeek != DayOfWeek.Monday)
            weekStart = weekStart.AddDays(-1);

        var plan = new StudyPlan
        {
            OwnerId = ownerId,
            WorkspaceId = req.WorkspaceId,
            Title = "Weekly Study Plan",
            WeekStartUtc = weekStart,
            WeeklyMinutesTarget = Math.Clamp(req.WeeklyMinutesTarget, 60, 7 * 8 * 60),
            CreatedAtUtc = DateTime.UtcNow,
            AiRoadmapJson = null
        };

        _db.StudyPlans.Add(plan);
        await _db.SaveChangesAsync(ct);

        // Inputs: active habits + planned actions (non-completed)
        var habits = await _db.Habits.AsNoTracking()
            .Where(h => h.OwnerId == ownerId && h.IsActive && (!req.WorkspaceId.HasValue || h.WorkspaceId == req.WorkspaceId))
            .OrderBy(h => h.Title)
            .Take(20)
            .ToListAsync(ct);

        var actions = await _db.Actions.AsNoTracking()
            .Where(a => a.OwnerId == ownerId && a.Status != "Completed" && (!req.WorkspaceId.HasValue || a.WorkspaceId == req.WorkspaceId))
            .OrderBy(a => a.DueAtUtc ?? DateTime.MaxValue)
            .ThenBy(a => a.Priority)
            .Take(20)
            .ToListAsync(ct);

        // Simple allocator: 5 days focus, 2 days review.
        var days = Enumerable.Range(0, 7).Select(i => weekStart.AddDays(i)).ToArray();
        var perDay = Math.Max(15, plan.WeeklyMinutesTarget / 7);

        var items = new List<StudyPlanItem>();
        var habitCycle = habits.Count == 0 ? null : habits.Select(h => new
        {
            h.Id,
            h.Title,
            Minutes = GuessHabitMinutes(h.Frequency, h.TargetCount)
        }).ToList();

        var actionQueue = actions.Select(a => new
        {
            a.Id,
            a.Title,
            Minutes = 30,
            Notes = a.Description
        }).ToList();

        var hi = 0;
        foreach (var d in days)
        {
            var remaining = perDay;

            // 1 habit focus item
            if (habitCycle != null && habitCycle.Count > 0)
            {
                var h = habitCycle[hi % habitCycle.Count];
                hi++;
                var mins = Math.Min(remaining, Math.Clamp(h.Minutes, 10, 45));
                if (mins > 0)
                {
                    items.Add(new StudyPlanItem
                    {
                        StudyPlanId = plan.Id,
                        OwnerId = ownerId,
                        ScheduledOnUtc = d,
                        Title = $"Habit: {h.Title}",
                        Minutes = mins,
                        ItemType = "Habit",
                        SourceEntityType = "Habit",
                        SourceEntityId = h.Id,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                    remaining -= mins;
                }
            }

            // Then fill with actions
            while (remaining >= 15 && actionQueue.Count > 0)
            {
                var a = actionQueue[0];
                actionQueue.RemoveAt(0);
                var mins = Math.Min(remaining, 45);
                items.Add(new StudyPlanItem
                {
                    StudyPlanId = plan.Id,
                    OwnerId = ownerId,
                    ScheduledOnUtc = d,
                    Title = $"Action: {a.Title}",
                    Notes = a.Notes,
                    Minutes = mins,
                    ItemType = "Action",
                    SourceEntityType = "Action",
                    SourceEntityId = a.Id,
                    CreatedAtUtc = DateTime.UtcNow
                });
                remaining -= mins;
            }

            // Add review slot on Sat/Sun
            if ((d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) && remaining >= 15)
            {
                items.Add(new StudyPlanItem
                {
                    StudyPlanId = plan.Id,
                    OwnerId = ownerId,
                    ScheduledOnUtc = d,
                    Title = "Review & reflect",
                    Notes = "Summarize what you learned, update next week, and capture insights.",
                    Minutes = Math.Min(remaining, 30),
                    ItemType = "Review",
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        _db.StudyPlanItems.AddRange(items);
        await _db.SaveChangesAsync(ct);

        // Optional AI roadmap (best effort)
        plan.AiRoadmapJson = await TryGenerateRoadmapJsonAsync(ownerId, req, habits, actions, ct);
        plan.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return plan;
    }

    private static int GuessHabitMinutes(string? frequency, int targetCount)
    {
        var f = (frequency ?? "").Trim();
        return f switch
        {
            "Daily" => Math.Clamp(10 * Math.Max(1, targetCount), 10, 45),
            "Weekly" => Math.Clamp(20 * Math.Max(1, targetCount), 20, 60),
            _ => 20
        };
    }

    private async Task<string?> TryGenerateRoadmapJsonAsync(
        string ownerId,
        GenerateRequest req,
        IList<Models.Habit> habits,
        IList<Models.ActionItem> actions,
        CancellationToken ct)
    {
        if (!_ai.IsConfigured) return null;

        // Keep payload small.
        var payload = new
        {
            intent = "study_planner_roadmap",
            nowUtc = DateTime.UtcNow,
            req.WorkspaceId,
            weekStartUtc = req.WeekStartUtc?.Date,
            weeklyMinutesTarget = req.WeeklyMinutesTarget,
            goal = string.IsNullOrWhiteSpace(req.Goal) ? null : req.Goal,
            habits = habits.Take(12).Select(h => new { h.Title, h.Frequency, h.TargetCount }),
            actions = actions.Take(12).Select(a => new { a.Title, a.DueAtUtc, a.Priority })
        };

        var systemPrompt =
            "You are a study planner and roadmap designer. Output STRICT JSON only. " +
            "Create a practical 4-week roadmap and focus suggestions based on the user's goal, habits, and actions. " +
            "Return JSON matching this schema: {\"roadmap4Weeks\":[{\"week\":number,\"focus\":string,\"milestones\":[string]}],\"tips\":[string]}";

        var inputJson = JsonSerializer.Serialize(payload);
        try
        {
            return await _ai.CreateJsonInsightsAsync(systemPrompt, inputJson, ct);
        }
        catch
        {
            return null;
        }
    }
}
