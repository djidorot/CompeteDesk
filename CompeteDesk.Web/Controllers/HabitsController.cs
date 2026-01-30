using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompeteDesk.Data;
using CompeteDesk.Models;
using CompeteDesk.Services.Habits;
using CompeteDesk.ViewModels.Habits;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompeteDesk.Controllers;

[Authorize]
public class HabitsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly HabitsAiService _ai;

    public HabitsController(ApplicationDbContext db, UserManager<IdentityUser> userManager, HabitsAiService ai)
    {
        _db = db;
        _userManager = userManager;
        _ai = ai;
    }

    private async Task<string> GetUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id ?? string.Empty;
    }

    private static string NormalizeFrequency(string? value)
    {
        var v = (value ?? "").Trim();
        if (v.Equals("Weekly", StringComparison.OrdinalIgnoreCase)) return "Weekly";
        return "Daily";
    }

    private static DateTime UtcDateToday() => DateTime.UtcNow.Date;

    private static (DateTime Start, DateTime EndExclusive) GetPeriodUtc(string frequency, DateTime todayUtcDate)
    {
        if (string.Equals(frequency, "Weekly", StringComparison.OrdinalIgnoreCase))
        {
            // ISO-ish week starting Monday
            var dow = (int)todayUtcDate.DayOfWeek; // Sunday=0
            var delta = (dow + 6) % 7; // Monday=0
            var start = todayUtcDate.AddDays(-delta);
            return (start, start.AddDays(7));
        }

        return (todayUtcDate, todayUtcDate.AddDays(1));
    }

    // GET: /Habits
    public async Task<IActionResult> Index(string? q, int? workspaceId, int? strategyId, string? frequency)
    {
        ViewData["Title"] = "Habits";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var vm = new HabitsIndexViewModel
        {
            Query = q?.Trim() ?? "",
            WorkspaceId = workspaceId,
            StrategyId = strategyId,
            Frequency = (frequency ?? "").Trim(),
            IsAiConfigured = _ai.IsConfigured
        };

        vm.Workspaces = await _db.Workspaces.AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .OrderBy(x => x.Name)
            .Select(x => new ValueTuple<int, string>(x.Id, x.Name))
            .ToListAsync();

        var strategiesQuery = _db.Strategies.AsNoTracking()
            .Where(x => x.OwnerId == userId);

        if (workspaceId.HasValue)
            strategiesQuery = strategiesQuery.Where(x => x.WorkspaceId == workspaceId.Value);

        vm.Strategies = await strategiesQuery
            .OrderBy(x => x.Name)
            .Select(x => new ValueTuple<int, string>(x.Id, x.Name))
            .ToListAsync();

        var habitsQuery = _db.Habits.AsNoTracking()
            .Where(x => x.OwnerId == userId);

        if (!string.IsNullOrWhiteSpace(vm.Query))
        {
            var term = vm.Query;
            habitsQuery = habitsQuery.Where(x =>
                x.Title.Contains(term) ||
                (x.Description != null && x.Description.Contains(term)));
        }

        if (workspaceId.HasValue) habitsQuery = habitsQuery.Where(x => x.WorkspaceId == workspaceId.Value);
        if (strategyId.HasValue) habitsQuery = habitsQuery.Where(x => x.StrategyId == strategyId.Value);
        if (!string.IsNullOrWhiteSpace(vm.Frequency)) habitsQuery = habitsQuery.Where(x => x.Frequency == vm.Frequency);

        
        // Load base habits (active first)
        var habits = await habitsQuery
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Frequency) // Daily first
            .ThenBy(x => x.Title)
            .ToListAsync();

        // If the user has no habits yet, render an empty page (avoid Min/Max on empty sequences).
        if (habits.Count == 0)
        {
            vm.Habits = new();
            return View(vm);
        }

        var today = UtcDateToday();
// Preload workspace/strategy names
        var wsMap = vm.Workspaces.ToDictionary(x => x.Id, x => x.Name);
        var stMap = vm.Strategies.ToDictionary(x => x.Id, x => x.Name);

        // Compute periods and pull checkins for all habits (today + each habit's period)
        var habitIds = habits.Select(h => h.Id).ToList();
        var periodWindows = habits.ToDictionary(
            h => h.Id,
            h => GetPeriodUtc(h.Frequency, today)
        );

        // Determine global min/max for one query window
        var minStart = periodWindows.Values.Min(x => x.Start);
        var maxEnd = periodWindows.Values.Max(x => x.EndExclusive);

        var checkins = await _db.HabitCheckins.AsNoTracking()
            .Where(c => c.OwnerId == userId && habitIds.Contains(c.HabitId))
            .Where(c => c.OccurredOnUtc >= minStart && c.OccurredOnUtc < maxEnd)
            .ToListAsync();

        // Group by habit + day
        var byHabit = checkins
            .GroupBy(c => c.HabitId)
            .ToDictionary(
                g => g.Key,
                g => g.ToList()
            );

        vm.Habits = habits.Select(h =>
        {
            var window = periodWindows[h.Id];
            var list = byHabit.TryGetValue(h.Id, out var l) ? l : new List<HabitCheckin>();

            var periodCount = list
                .Where(c => c.OccurredOnUtc >= window.Start && c.OccurredOnUtc < window.EndExclusive)
                .Sum(c => c.Count);

            var todayCount = list
                .Where(c => c.OccurredOnUtc == today)
                .Sum(c => c.Count);

            return new HabitListItemViewModel
            {
                Id = h.Id,
                WorkspaceId = h.WorkspaceId,
                WorkspaceName = wsMap.TryGetValue(h.WorkspaceId, out var wsn) ? wsn : $"#{h.WorkspaceId}",
                StrategyId = h.StrategyId,
                StrategyName = (h.StrategyId.HasValue && stMap.TryGetValue(h.StrategyId.Value, out var stn)) ? stn : null,
                Title = h.Title,
                Description = h.Description,
                Frequency = h.Frequency,
                TargetCount = h.TargetCount,
                IsActive = h.IsActive,
                PeriodCount = periodCount,
                TodayCount = todayCount,
                PeriodStartUtc = window.Start,
                PeriodEndUtc = window.EndExclusive.AddDays(-1)
            };
        }).ToList();

        return View(vm);
    }

    // ------------------------------------------------------------
    // AI: Suggest habits (returns JSON for the UI)
    // ------------------------------------------------------------

    public sealed class HabitSuggestionDto
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string Frequency { get; set; } = "Daily";
        public int TargetCount { get; set; } = 1;
        public string? Cue { get; set; }
        public string? Routine { get; set; }
        public string? Reward { get; set; }
        public string? Rationale { get; set; }
    }

    public sealed class AiSuggestRequest
    {
        public int WorkspaceId { get; set; }
        public int? StrategyId { get; set; }
        public string? Goal { get; set; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AiSuggest([FromForm] AiSuggestRequest req)
    {
        if (!_ai.IsConfigured)
            return Json(new { ok = false, error = "AI is not configured. Add OpenAI settings in appsettings or user secrets." });

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Json(new { ok = false, error = "Not authenticated." });

        if (req.WorkspaceId <= 0)
            return Json(new { ok = false, error = "Please select a Workspace." });

        var doc = await _ai.SuggestAsync(userId, req.WorkspaceId, req.StrategyId, req.Goal, HttpContext.RequestAborted);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var err))
            return Json(new { ok = false, error = err.GetString() ?? "AI error" });

        return Json(new { ok = true, data = root });
    }

    public sealed class AiCreateRequest
    {
        public int WorkspaceId { get; set; }
        public int? StrategyId { get; set; }
        public List<HabitSuggestionDto> Suggestions { get; set; } = new();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AiCreate([FromBody] AiCreateRequest req)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (req.WorkspaceId <= 0 || req.Suggestions.Count == 0)
            return BadRequest(new { ok = false, error = "No suggestions provided." });

        // Validate workspace belongs to user
        var wsOk = await _db.Workspaces.AsNoTracking().AnyAsync(x => x.OwnerId == userId && x.Id == req.WorkspaceId);
        if (!wsOk)
            return BadRequest(new { ok = false, error = "Workspace not found." });

        // Strategy is optional but if provided, validate
        if (req.StrategyId.HasValue)
        {
            var stOk = await _db.Strategies.AsNoTracking().AnyAsync(x => x.OwnerId == userId && x.Id == req.StrategyId.Value);
            if (!stOk)
                return BadRequest(new { ok = false, error = "Strategy not found." });
        }

        var now = DateTime.UtcNow;
        var created = 0;

        foreach (var s in req.Suggestions.Take(12))
        {
            var title = (s.Title ?? "").Trim();
            if (string.IsNullOrWhiteSpace(title)) continue;

            var freq = NormalizeFrequency(s.Frequency);
            var target = s.TargetCount <= 0 ? 1 : Math.Min(s.TargetCount, 99);

            // Avoid duplicates by title+frequency inside this workspace/strategy
            var exists = await _db.Habits.AnyAsync(h => h.OwnerId == userId
                                                        && h.WorkspaceId == req.WorkspaceId
                                                        && h.StrategyId == req.StrategyId
                                                        && h.Frequency == freq
                                                        && h.Title == title);
            if (exists) continue;

            var descParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(s.Description)) descParts.Add(s.Description!.Trim());
            if (!string.IsNullOrWhiteSpace(s.Cue)) descParts.Add($"Cue: {s.Cue!.Trim()}");
            if (!string.IsNullOrWhiteSpace(s.Routine)) descParts.Add($"Routine: {s.Routine!.Trim()}");
            if (!string.IsNullOrWhiteSpace(s.Reward)) descParts.Add($"Reward: {s.Reward!.Trim()}");
            if (!string.IsNullOrWhiteSpace(s.Rationale)) descParts.Add($"Why it matters: {s.Rationale!.Trim()}");

            var habit = new Habit
            {
                OwnerId = userId,
                WorkspaceId = req.WorkspaceId,
                StrategyId = req.StrategyId,
                Title = title,
                Description = descParts.Count == 0 ? null : string.Join("\n", descParts),
                Frequency = freq,
                TargetCount = target,
                IsActive = true,
                CreatedAtUtc = now
            };

            _db.Habits.Add(habit);
            created++;
        }

        if (created > 0)
            await _db.SaveChangesAsync();

        return Ok(new { ok = true, created });
    }

    // GET: /Habits/Create
    public async Task<IActionResult> Create(int? workspaceId, int? strategyId, string? frequency)
    {
        ViewData["Title"] = "New Habit";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var vm = new HabitEditViewModel
        {
            Habit = new Habit
            {
                WorkspaceId = workspaceId ?? 0,
                StrategyId = strategyId,
                Frequency = NormalizeFrequency(frequency),
                TargetCount = 1,
                IsActive = true
            }
        };

        vm.Workspaces = await _db.Workspaces.AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .OrderBy(x => x.Name)
            .Select(x => new ValueTuple<int, string>(x.Id, x.Name))
            .ToListAsync();

        var strategiesQuery = _db.Strategies.AsNoTracking()
            .Where(x => x.OwnerId == userId);

        if (workspaceId.HasValue)
            strategiesQuery = strategiesQuery.Where(x => x.WorkspaceId == workspaceId.Value);

        vm.Strategies = await strategiesQuery
            .OrderBy(x => x.Name)
            .Select(x => new ValueTuple<int, string>(x.Id, x.Name))
            .ToListAsync();

        return View(vm);
    }

    // POST: /Habits/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(HabitEditViewModel vm)
    {
        ViewData["Title"] = "New Habit";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        vm.Habit.OwnerId = userId;
        vm.Habit.Frequency = NormalizeFrequency(vm.Habit.Frequency);
        vm.Habit.TargetCount = Math.Max(1, vm.Habit.TargetCount);
        vm.Habit.CreatedAtUtc = DateTime.UtcNow;
        vm.Habit.UpdatedAtUtc = DateTime.UtcNow;

        if (vm.Habit.StrategyId.HasValue && vm.Habit.StrategyId.Value <= 0)
            vm.Habit.StrategyId = null;

        // Validate workspace belongs to user
        var wsOk = await _db.Workspaces.AsNoTracking().AnyAsync(x => x.Id == vm.Habit.WorkspaceId && x.OwnerId == userId);
        if (!wsOk) ModelState.AddModelError("Habit.WorkspaceId", "Select a valid workspace.");

        if (!ModelState.IsValid)
        {
            // repopulate dropdowns
            vm.Workspaces = await _db.Workspaces.AsNoTracking()
                .Where(x => x.OwnerId == userId)
                .OrderBy(x => x.Name)
                .Select(x => new ValueTuple<int, string>(x.Id, x.Name))
                .ToListAsync();

            vm.Strategies = await _db.Strategies.AsNoTracking()
                .Where(x => x.OwnerId == userId)
                .OrderBy(x => x.Name)
                .Select(x => new ValueTuple<int, string>(x.Id, x.Name))
                .ToListAsync();

            return View(vm);
        }

        _db.Habits.Add(vm.Habit);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { workspaceId = vm.Habit.WorkspaceId });
    }

    // GET: /Habits/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        ViewData["Title"] = "Habit Details";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var habit = await _db.Habits.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);

        if (habit == null) return NotFound();

        var today = UtcDateToday();
        var window = GetPeriodUtc(habit.Frequency, today);

        var periodCount = await _db.HabitCheckins.AsNoTracking()
            .Where(c => c.OwnerId == userId && c.HabitId == habit.Id)
            .Where(c => c.OccurredOnUtc >= window.Start && c.OccurredOnUtc < window.EndExclusive)
            .SumAsync(c => (int?)c.Count) ?? 0;

        ViewBag.PeriodStartUtc = window.Start;
        ViewBag.PeriodEndUtc = window.EndExclusive.AddDays(-1);
        ViewBag.PeriodCount = periodCount;

        var workspaceName = await _db.Workspaces.AsNoTracking()
            .Where(w => w.Id == habit.WorkspaceId && w.OwnerId == userId)
            .Select(w => w.Name)
            .FirstOrDefaultAsync();

        var strategyName = habit.StrategyId.HasValue
            ? await _db.Strategies.AsNoTracking()
                .Where(s => s.Id == habit.StrategyId.Value && s.OwnerId == userId)
                .Select(s => s.Name)
                .FirstOrDefaultAsync()
            : null;

        ViewBag.WorkspaceName = workspaceName ?? $"#{habit.WorkspaceId}";
        ViewBag.StrategyName = strategyName;

        return View(habit);
    }

    // GET: /Habits/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        ViewData["Title"] = "Edit Habit";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var habit = await _db.Habits.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        if (habit == null) return NotFound();

        var vm = new HabitEditViewModel { Habit = habit };

        vm.Workspaces = await _db.Workspaces.AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .OrderBy(x => x.Name)
            .Select(x => new ValueTuple<int, string>(x.Id, x.Name))
            .ToListAsync();

        vm.Strategies = await _db.Strategies.AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .OrderBy(x => x.Name)
            .Select(x => new ValueTuple<int, string>(x.Id, x.Name))
            .ToListAsync();

        return View(vm);
    }

    // POST: /Habits/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, HabitEditViewModel vm)
    {
        if (id != vm.Habit.Id) return NotFound();

        ViewData["Title"] = "Edit Habit";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var habit = await _db.Habits.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        if (habit == null) return NotFound();

        vm.Habit.Frequency = NormalizeFrequency(vm.Habit.Frequency);
        vm.Habit.TargetCount = Math.Max(1, vm.Habit.TargetCount);
        if (vm.Habit.StrategyId.HasValue && vm.Habit.StrategyId.Value <= 0) vm.Habit.StrategyId = null;

        var wsOk = await _db.Workspaces.AsNoTracking().AnyAsync(x => x.Id == vm.Habit.WorkspaceId && x.OwnerId == userId);
        if (!wsOk) ModelState.AddModelError("Habit.WorkspaceId", "Select a valid workspace.");

        if (!ModelState.IsValid)
        {
            vm.Workspaces = await _db.Workspaces.AsNoTracking()
                .Where(x => x.OwnerId == userId)
                .OrderBy(x => x.Name)
                .Select(x => new ValueTuple<int, string>(x.Id, x.Name))
                .ToListAsync();

            vm.Strategies = await _db.Strategies.AsNoTracking()
                .Where(x => x.OwnerId == userId)
                .OrderBy(x => x.Name)
                .Select(x => new ValueTuple<int, string>(x.Id, x.Name))
                .ToListAsync();

            return View(vm);
        }

        habit.Title = vm.Habit.Title;
        habit.Description = vm.Habit.Description;
        habit.Frequency = vm.Habit.Frequency;
        habit.TargetCount = vm.Habit.TargetCount;
        habit.IsActive = vm.Habit.IsActive;
        habit.WorkspaceId = vm.Habit.WorkspaceId;
        habit.StrategyId = vm.Habit.StrategyId;
        habit.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { workspaceId = habit.WorkspaceId });
    }

    // GET: /Habits/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        ViewData["Title"] = "Delete Habit";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var habit = await _db.Habits.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);

        return habit == null ? NotFound() : View(habit);
    }

    // POST: /Habits/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var userId = await GetUserIdAsync();
        var habit = await _db.Habits.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        if (habit == null) return NotFound();

        _db.Habits.Remove(habit);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // POST: /Habits/Checkin/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkin(int id)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var habit = await _db.Habits.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        if (habit == null) return NotFound();

        var today = UtcDateToday();

        var existing = await _db.HabitCheckins
            .FirstOrDefaultAsync(c => c.HabitId == id && c.OwnerId == userId && c.OccurredOnUtc == today);

        if (existing == null)
        {
            _db.HabitCheckins.Add(new HabitCheckin
            {
                HabitId = id,
                OwnerId = userId,
                OccurredOnUtc = today,
                Count = 1,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            // For daily habits, cap at target to keep UI meaningful.
            var next = existing.Count + 1;
            if (habit.Frequency == "Daily")
                next = Math.Min(habit.TargetCount, next);

            existing.Count = next;
        }

        await _db.SaveChangesAsync();

        // Keep user on index with filters (workspace/strategy)
        return RedirectToAction(nameof(Index), new { workspaceId = habit.WorkspaceId });
    }

    // POST: /Habits/ToggleActive/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var habit = await _db.Habits.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        if (habit == null) return NotFound();

        habit.IsActive = !habit.IsActive;
        habit.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { workspaceId = habit.WorkspaceId });
    }
}
