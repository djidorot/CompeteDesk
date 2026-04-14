using CompeteDesk.Models;
using CompeteDesk.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;

namespace CompeteDesk.Services.Exports;

public sealed class ExportReportService
{
    private readonly ApplicationDbContext _db;
    private readonly WorkspaceAccessService _workspaceAccess;

    public ExportReportService(ApplicationDbContext db, WorkspaceAccessService workspaceAccess)
    {
        _db = db;
        _workspaceAccess = workspaceAccess;
    }

    private IQueryable<int> AccessibleWorkspaceIds(string userId)
        => _workspaceAccess.AccessibleWorkspaceIds(userId);

    private IQueryable<Strategy> StrategiesScope(string userId)
    {
        var accessibleWorkspaceIds = AccessibleWorkspaceIds(userId);
        return _db.Strategies.AsNoTracking().Where(s => s.OwnerId == userId || (s.WorkspaceId != null && accessibleWorkspaceIds.Contains(s.WorkspaceId.Value)));
    }

    private IQueryable<ActionItem> ActionsScope(string userId)
    {
        var accessibleWorkspaceIds = AccessibleWorkspaceIds(userId);
        return _db.Actions.AsNoTracking().Where(a => a.OwnerId == userId || (a.WorkspaceId != null && accessibleWorkspaceIds.Contains(a.WorkspaceId.Value)));
    }

    private IQueryable<Habit> HabitsScope(string userId)
    {
        var accessibleWorkspaceIds = AccessibleWorkspaceIds(userId);
        return _db.Habits.AsNoTracking().Where(h => h.OwnerId == userId || (h.WorkspaceId != null && h.WorkspaceId > 0 && accessibleWorkspaceIds.Contains(h.WorkspaceId.Value)));
    }

    public async Task<byte[]> ExportCompetencySummaryPdfAsync(string ownerId, int? workspaceId, CancellationToken ct)
    {
        // In CompeteDesk, "competency" maps best to a Workspace execution summary.
        var ws = workspaceId.HasValue
            ? await _workspaceAccess.GetAccessibleWorkspaceAsync(ownerId, workspaceId.Value, ct)
            : null;

        var title = ws == null ? "CompeteDesk Summary" : $"Workspace Summary: {ws.Name}";

        var strategies = await StrategiesScope(ownerId)
            .Where(s => !workspaceId.HasValue || s.WorkspaceId == workspaceId)
            .OrderByDescending(s => s.Priority)
            .ThenBy(s => s.Name)
            .Take(15)
            .ToListAsync(ct);

        var actions = await ActionsScope(ownerId)
            .Where(a => !workspaceId.HasValue || a.WorkspaceId == workspaceId)
            .ToListAsync(ct);

        var habits = await HabitsScope(ownerId)
            .Where(h => h.IsActive && (!workspaceId.HasValue || h.WorkspaceId == workspaceId))
            .OrderBy(h => h.Title)
            .Take(15)
            .ToListAsync(ct);

        var done = actions.Count(a => a.Status == "Completed");
        var open = actions.Count - done;

        var lines = new List<string>
        {
            ws == null ? "" : $"Workspace: {ws.Name}",
            // Workspace currently stores its "goal" as Description.
            ws == null ? "" : $"Goal: {ws.Description}",
            "",
            $"Strategies: {strategies.Count}",
            $"Actions: {actions.Count} (Open: {open}, Completed: {done})",
            $"Active habits: {habits.Count}",
            "",
            "Top Strategies:"
        };

        lines.AddRange(strategies.Select(s => $"- {s.Name} ({s.Category})"));
        lines.Add(" ");
        lines.Add("Next Actions:");

        lines.AddRange(actions
            .Where(a => a.Status != "Completed")
            .OrderBy(a => a.DueAtUtc ?? DateTime.MaxValue)
            .ThenBy(a => a.Priority)
            .Take(10)
            .Select(a => $"- {a.Title}" + (a.DueAtUtc.HasValue ? $" (Due {a.DueAtUtc.Value:yyyy-MM-dd})" : "")));

        lines.Add(" ");
        lines.Add("Active Habits:");
        lines.AddRange(habits.Select(h => $"- {h.Title} ({h.Frequency}, target {h.TargetCount})"));

        return SimplePdfWriter.CreateTextPdf(title, lines);
    }

    public async Task<byte[]> ExportProgressReportPdfAsync(string ownerId, int? workspaceId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var from = now.Date.AddDays(-30);

        var ws = workspaceId.HasValue
            ? await _workspaceAccess.GetAccessibleWorkspaceAsync(ownerId, workspaceId.Value, ct)
            : null;

        var title = ws == null ? "Progress Report (Last 30 Days)" : $"Progress Report: {ws.Name} (Last 30 Days)";

        var actionTotal = await ActionsScope(ownerId)
            .Where(a => !workspaceId.HasValue || a.WorkspaceId == workspaceId)
            .CountAsync(ct);

        var actionDone = await ActionsScope(ownerId)
            .Where(a => a.Status == "Completed" && (!workspaceId.HasValue || a.WorkspaceId == workspaceId))
            .CountAsync(ct);

        var checkins = await _db.HabitCheckins.AsNoTracking()
            .Where(c => c.OccurredOnUtc >= from && (!workspaceId.HasValue || _db.Habits.Any(h => h.Id == c.HabitId && h.WorkspaceId == workspaceId)))
            .ToListAsync(ct);

        var checkinDays = checkins.Select(c => c.OccurredOnUtc.Date).Distinct().Count();
        var totalCheckins = checkins.Sum(c => c.Count);

        // KeyMetricEntry uses DateUtc (stored at midnight UTC) rather than ObservedAtUtc.
        // Also, label/unit come from the Definition navigation.
        var metrics = await _db.KeyMetricEntries.AsNoTracking()
            .Include(m => m.Definition)
            .Where(m => m.OwnerId == ownerId && m.DateUtc >= from && (!workspaceId.HasValue || m.WorkspaceId == workspaceId))
            .OrderByDescending(m => m.DateUtc)
            .Take(10)
            .ToListAsync(ct);

        var lines = new List<string>
        {
            $"Window: {from:yyyy-MM-dd} to {now:yyyy-MM-dd} (UTC)",
            "",
            $"Actions: {actionTotal} total | {actionDone} completed",
            $"Habit activity days: {checkinDays} days | total check-ins: {totalCheckins}",
            "",
            "Latest key metric entries:",
        };

        foreach (var m in metrics)
        {
            var label = m.Definition?.DisplayName ?? m.Definition?.Key ?? $"Metric #{m.DefinitionId}";
            var unit = m.Definition?.Unit ?? string.Empty;
            lines.Add($"- {m.DateUtc:yyyy-MM-dd}: {label} = {m.Value} {unit}".Trim());
        }

        lines.Add(" ");
        lines.Add("Suggested next steps:");
        lines.Add(actionDone == 0
            ? "- Complete 1 small action this week to build momentum."
            : "- Raise the bar: ship 1 higher-impact action.");
        lines.Add(checkinDays < 7
            ? "- Aim for 3+ habit days this week (consistency beats intensity)."
            : "- Protect your streak: keep daily minimums.");

        return SimplePdfWriter.CreateTextPdf(title, lines);
    }

    public async Task<byte[]> ExportStrategiesPdfAsync(string ownerId, int? workspaceId, CancellationToken ct)
    {
        var strategies = await StrategiesScope(ownerId)
            .Where(s => !workspaceId.HasValue || s.WorkspaceId == workspaceId)
            .OrderByDescending(s => s.Priority)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);

        var lines = new List<string>
        {
            $"Strategies exported: {strategies.Count}",
            ""
        };

        lines.AddRange(strategies.Select(s =>
            $"- {s.Name} | {s.Status} | {s.ProgressPercent}% | Due {(s.DeadlineUtc.HasValue ? s.DeadlineUtc.Value.ToString("yyyy-MM-dd") : "—")} | Tags {s.Tags ?? "—"}"));

        return SimplePdfWriter.CreateTextPdf("Strategies Export", lines);
    }

    public async Task<byte[]> ExportStrategiesCsvAsync(string ownerId, int? workspaceId, CancellationToken ct)
    {
        var rows = await StrategiesScope(ownerId)
            .Where(s => !workspaceId.HasValue || s.WorkspaceId == workspaceId)
            .OrderByDescending(s => s.Priority)
            .ThenBy(s => s.Name)
            .Select(s => new
            {
                s.Name,
                s.Category,
                s.Status,
                s.ProgressPercent,
                s.Priority,
                s.DeadlineUtc,
                s.ReminderUtc,
                s.Tags
            })
            .ToListAsync(ct);

        static string Csv(string? value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Name,Category,Status,ProgressPercent,Priority,DeadlineUtc,ReminderUtc,Tags");

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                Csv(r.Name),
                Csv(r.Category),
                Csv(r.Status),
                r.ProgressPercent.ToString(),
                r.Priority.ToString(),
                Csv(r.DeadlineUtc?.ToString("u")),
                Csv(r.ReminderUtc?.ToString("u")),
                Csv(r.Tags)));
        }

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> ExportMonthlySummaryPdfAsync(string ownerId, int? workspaceId, CancellationToken ct)
    {
        var from = DateTime.UtcNow.Date.AddDays(-30);
        var strategies = await StrategiesScope(ownerId)
            .Where(s => !workspaceId.HasValue || s.WorkspaceId == workspaceId)
            .ToListAsync(ct);

        var created = strategies.Count(s => s.CreatedAtUtc >= from);
        var completed = strategies.Count(s => s.Status == "Completed");
        var avgProgress = strategies.Count == 0 ? 0 : (int)Math.Round(strategies.Average(s => s.ProgressPercent));
        var overdue = strategies.Count(s => s.DeadlineUtc.HasValue && s.DeadlineUtc.Value < DateTime.UtcNow && s.Status != "Completed");

        var lines = new List<string>
        {
            $"Window: {from:yyyy-MM-dd} to {DateTime.UtcNow:yyyy-MM-dd}",
            $"New strategies: {created}",
            $"Completed strategies: {completed}",
            $"Average progress: {avgProgress}%",
            $"Overdue strategies: {overdue}",
            "",
            "Top priorities:"
        };

        lines.AddRange(strategies
            .OrderByDescending(s => s.Priority)
            .Take(10)
            .Select(s => $"- {s.Name} ({s.Status}, {s.ProgressPercent}%)"));

        return SimplePdfWriter.CreateTextPdf("Monthly Dashboard Summary", lines);
    }
}