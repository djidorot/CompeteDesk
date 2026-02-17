using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Services.StudyPlanner;

namespace CompeteDesk.Controllers;

[Authorize]
public sealed class StudyPlannerController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly StudyPlannerService _planner;

    public StudyPlannerController(ApplicationDbContext db, UserManager<IdentityUser> userManager, StudyPlannerService planner)
    {
        _db = db;
        _userManager = userManager;
        _planner = planner;
    }

    private async Task<string?> GetUserIdAsync() => (await _userManager.GetUserAsync(User))?.Id;

    public sealed class IndexVm
    {
        public int? WorkspaceId { get; set; }
        public string? WorkspaceName { get; set; }
        public DateTime WeekStartUtc { get; set; }
        public int WeeklyMinutesTarget { get; set; } = 300;
        public string? Goal { get; set; }
        public bool AiEnabled { get; set; }
        public object? Roadmap { get; set; }
        public Models.StudyPlan? LatestPlan { get; set; }
        public ILookup<DateTime, Models.StudyPlanItem>? ItemsByDay { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? workspaceId, CancellationToken ct)
    {
        ViewData["Title"] = "Study Planner";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var ws = workspaceId.HasValue
            ? await _db.Workspaces.AsNoTracking().FirstOrDefaultAsync(x => x.Id == workspaceId && x.OwnerId == userId, ct)
            : null;

        var latest = await _db.StudyPlans.AsNoTracking()
            .Where(p => p.OwnerId == userId && (!workspaceId.HasValue || p.WorkspaceId == workspaceId))
            .OrderByDescending(p => p.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        // Keep a consistent type (List<T>) regardless of whether a plan exists.
        var items = latest == null
            ? new List<Models.StudyPlanItem>()
            : await _db.StudyPlanItems.AsNoTracking()
                .Where(i => i.OwnerId == userId && i.StudyPlanId == latest.Id)
                .OrderBy(i => i.ScheduledOnUtc)
                .ThenBy(i => i.ItemType)
                .ToListAsync(ct);

        object? roadmap = null;
        if (latest?.AiRoadmapJson is { Length: > 0 })
        {
            try { roadmap = System.Text.Json.JsonSerializer.Deserialize<object>(latest.AiRoadmapJson); } catch { }
        }

        var monday = DateTime.UtcNow.Date;
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(-1);

        var vm = new IndexVm
        {
            WorkspaceId = workspaceId,
            WorkspaceName = ws?.Name,
            WeekStartUtc = monday,
            WeeklyMinutesTarget = 300,
            // Workspace currently stores its "goal" as Description.
            Goal = ws?.Description,
            AiEnabled = _planner.IsAiConfigured,
            LatestPlan = latest,
            ItemsByDay = items.ToLookup(x => x.ScheduledOnUtc.Date),
            Roadmap = roadmap
        };

        return View(vm);
    }

    [HttpPost]
    [Authorize(Policy = "CanEdit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(IndexVm vm, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var plan = await _planner.GenerateWeeklyPlanAsync(userId, new StudyPlannerService.GenerateRequest
        {
            WorkspaceId = vm.WorkspaceId,
            WeekStartUtc = vm.WeekStartUtc,
            WeeklyMinutesTarget = vm.WeeklyMinutesTarget,
            Goal = vm.Goal
        }, ct);

        TempData["ToastSuccess"] = "Study plan generated.";
        return RedirectToAction(nameof(Index), new { workspaceId = plan.WorkspaceId });
    }
}
