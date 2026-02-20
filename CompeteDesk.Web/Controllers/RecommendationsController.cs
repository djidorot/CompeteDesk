using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Models;
using CompeteDesk.Services;
using CompeteDesk.Services.Recommendations;

namespace CompeteDesk.Controllers;

[Authorize]
public sealed class RecommendationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RecommendationsService _recs;
    private readonly ActiveWorkspaceService _activeWs;

    public RecommendationsController(
        ApplicationDbContext db,
        UserManager<IdentityUser> userManager,
        RecommendationsService recs,
        ActiveWorkspaceService activeWs)
    {
        _db = db;
        _userManager = userManager;
        _recs = recs;
        _activeWs = activeWs;
    }

    private async Task<string?> GetUserIdAsync() => (await _userManager.GetUserAsync(User))?.Id;

    public sealed class IndexVm
    {
        public int? WorkspaceId { get; set; }
        public string? WorkspaceName { get; set; }
        public WorkspaceOpt[] Workspaces { get; set; } = System.Array.Empty<WorkspaceOpt>();
        public bool AiEnabled { get; set; }

        // Signals
        public int OpenActions { get; set; }
        public int OverdueActions { get; set; }
        public int ActiveHabits { get; set; }
        public int HabitCheckins14d { get; set; }
        public string? WeakestHabitTitle { get; set; }
        public int WeakestHabitCount14d { get; set; }
        public string[] TopBooks { get; set; } = System.Array.Empty<string>();

        public RecommendationsService.Recommendation[] Items { get; set; } = System.Array.Empty<RecommendationsService.Recommendation>();
    }

    public sealed class WorkspaceOpt
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? workspaceId, CancellationToken ct)
    {
        ViewData["Title"] = "Smart Recommendations";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        // Persist active workspace selection so other create flows keep using it.
        if (workspaceId.HasValue && workspaceId.Value > 0)
            _activeWs.PersistSelection(HttpContext, workspaceId.Value);

        var resolvedWorkspaceId = await _activeWs.ResolveAsync(HttpContext, userId, workspaceId, ct);

        // Workspace list for the picker
        var workspaces = await _db.Workspaces
            .AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .OrderBy(x => x.Name)
            .Select(x => new WorkspaceOpt { Id = x.Id, Name = x.Name })
            .ToArrayAsync(ct);

        var ws = resolvedWorkspaceId.HasValue
            ? await _db.Workspaces.AsNoTracking().FirstOrDefaultAsync(x => x.Id == resolvedWorkspaceId && x.OwnerId == userId, ct)
            : null;

        // --- Signals for summary cards
        var now = System.DateTime.UtcNow;
        var from = now.Date.AddDays(-14);

        var actionsQuery = _db.Actions.AsNoTracking()
            .Where(a => a.OwnerId == userId && a.Status != "Completed" && (!resolvedWorkspaceId.HasValue || a.WorkspaceId == resolvedWorkspaceId));

        var openActions = await actionsQuery.CountAsync(ct);
        var overdueActions = await actionsQuery.CountAsync(a => a.DueAtUtc.HasValue && a.DueAtUtc.Value.Date < now.Date, ct);

        var habits = await _db.Habits.AsNoTracking()
            .Where(h => h.OwnerId == userId && h.IsActive && (!resolvedWorkspaceId.HasValue || h.WorkspaceId == resolvedWorkspaceId))
            .OrderBy(h => h.Title)
            .Select(h => new { h.Id, h.Title })
            .ToListAsync(ct);

        var activeHabits = habits.Count;
        int habitCheckins14d = 0;
        string? weakestHabitTitle = null;
        int weakestHabitCount = 0;

        if (habits.Count > 0)
        {
            var habitIds = habits.Select(h => h.Id).ToArray();
            var checkins = await _db.HabitCheckins.AsNoTracking()
                .Where(c => c.OwnerId == userId && habitIds.Contains(c.HabitId) && c.OccurredOnUtc >= from)
                .GroupBy(c => c.HabitId)
                .Select(g => new { HabitId = g.Key, Total = g.Sum(x => x.Count) })
                .ToListAsync(ct);

            habitCheckins14d = checkins.Sum(x => x.Total);
            var byId = checkins.ToDictionary(x => x.HabitId, x => x.Total);

            var weakest = habits
                .Select(h => new { h.Title, Total = byId.TryGetValue(h.Id, out var t) ? t : 0 })
                .OrderBy(x => x.Total)
                .FirstOrDefault();

            if (weakest != null)
            {
                weakestHabitTitle = weakest.Title;
                weakestHabitCount = weakest.Total;
            }
        }

        var topBooks = await _db.Actions.AsNoTracking()
            .Where(a => a.OwnerId == userId && a.SourceBook != null && a.SourceBook != "" && (!resolvedWorkspaceId.HasValue || a.WorkspaceId == resolvedWorkspaceId))
            .GroupBy(a => a.SourceBook)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key!)
            .Take(3)
            .ToArrayAsync(ct);

        var items = (await _recs.GetAsync(userId, resolvedWorkspaceId, ct)).Take(8).ToArray();

        return View(new IndexVm
        {
            WorkspaceId = resolvedWorkspaceId,
            WorkspaceName = ws?.Name,
            Workspaces = workspaces,
            AiEnabled = _recs.IsAiConfigured,
            OpenActions = openActions,
            OverdueActions = overdueActions,
            ActiveHabits = activeHabits,
            HabitCheckins14d = habitCheckins14d,
            WeakestHabitTitle = weakestHabitTitle,
            WeakestHabitCount14d = weakestHabitCount,
            TopBooks = topBooks,
            Items = items
        });
    }

    [HttpPost]
    [Authorize(Policy = "CanEdit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateActionFromRecommendation(string title, string? note, int? workspaceId, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        title = (title ?? string.Empty).Trim();
        if (title.Length == 0) return BadRequest(new { ok = false, error = "Title is required." });
        if (title.Length > 200) title = title.Substring(0, 200);

        var resolvedWsId = await _activeWs.ResolveAsync(HttpContext, userId, workspaceId, ct);

        var item = new ActionItem
        {
            OwnerId = userId,
            WorkspaceId = resolvedWsId,
            Title = title,
            Description = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            Status = "Planned",
            Priority = 0,
            DueAtUtc = System.DateTime.UtcNow.AddDays(3),
            CreatedAtUtc = System.DateTime.UtcNow,
            UpdatedAtUtc = System.DateTime.UtcNow
        };

        _db.Actions.Add(item);
        await _db.SaveChangesAsync(ct);

        return Json(new
        {
            ok = true,
            id = item.Id,
            redirectUrl = Url.Action("Edit", "Actions", new { id = item.Id })
        });
    }
}
