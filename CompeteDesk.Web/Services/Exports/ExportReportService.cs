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

    public ExportReportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<byte[]> ExportCompetencySummaryPdfAsync(string ownerId, int? workspaceId, CancellationToken ct)
    {
        // In CompeteDesk, "competency" maps best to a Workspace execution summary.
        var ws = workspaceId.HasValue
            ? await _db.Workspaces.AsNoTracking().FirstOrDefaultAsync(x => x.Id == workspaceId && x.OwnerId == ownerId, ct)
            : null;

        var title = ws == null ? "CompeteDesk Summary" : $"Workspace Summary: {ws.Name}";

        var strategies = await _db.Strategies.AsNoTracking()
            .Where(s => s.OwnerId == ownerId && (!workspaceId.HasValue || s.WorkspaceId == workspaceId))
            .OrderByDescending(s => s.Priority)
            .ThenBy(s => s.Name)
            .Take(15)
            .ToListAsync(ct);

        var actions = await _db.Actions.AsNoTracking()
            .Where(a => a.OwnerId == ownerId && (!workspaceId.HasValue || a.WorkspaceId == workspaceId))
            .ToListAsync(ct);

        var habits = await _db.Habits.AsNoTracking()
            .Where(h => h.OwnerId == ownerId && h.IsActive && (!workspaceId.HasValue || h.WorkspaceId == workspaceId))
            .OrderBy(h => h.Title)
            .Take(15)
            .ToListAsync(ct);

        var done = actions.Count(a => a.Status == "Completed");
        var open = actions.Count - done;

        var lines = new List<string>
        {
            ws == null ? "" : $"Workspace: {ws.Name}",
            ws == null ? "" : $"Goal: {ws.Goal}",
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
            ? await _db.Workspaces.AsNoTracking().FirstOrDefaultAsync(x => x.Id == workspaceId && x.OwnerId == ownerId, ct)
            : null;

        var title = ws == null ? "Progress Report (Last 30 Days)" : $"Progress Report: {ws.Name} (Last 30 Days)";

        var actionTotal = await _db.Actions.AsNoTracking()
            .Where(a => a.OwnerId == ownerId && (!workspaceId.HasValue || a.WorkspaceId == workspaceId))
            .CountAsync(ct);

        var actionDone = await _db.Actions.AsNoTracking()
            .Where(a => a.OwnerId == ownerId && a.Status == "Completed" && (!workspaceId.HasValue || a.WorkspaceId == workspaceId))
            .CountAsync(ct);

        var checkins = await _db.HabitCheckins.AsNoTracking()
            .Where(c => c.OwnerId == ownerId && c.OccurredOnUtc >= from)
            .ToListAsync(ct);

        var checkinDays = checkins.Select(c => c.OccurredOnUtc.Date).Distinct().Count();
        var totalCheckins = checkins.Sum(c => c.Count);

        var metrics = await _db.KeyMetricEntries.AsNoTracking()
            .Where(m => m.OwnerId == ownerId && m.ObservedAtUtc >= from)
            .OrderByDescending(m => m.ObservedAtUtc)
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
            lines.Add($"- {m.ObservedAtUtc:yyyy-MM-dd}: {m.Label} = {m.Value} {m.Unit}".Trim());
        }

        lines.Add(" ");
        lines.Add("Suggested next steps:");
        lines.Add(actionDone == 0 ? "- Complete 1 small action this week to build momentum." : "- Raise the bar: ship 1 higher-impact action." );
        lines.Add(checkinDays < 7 ? "- Aim for 3+ habit days this week (consistency beats intensity)." : "- Protect your streak: keep daily minimums." );

        return SimplePdfWriter.CreateTextPdf(title, lines);
    }
}
