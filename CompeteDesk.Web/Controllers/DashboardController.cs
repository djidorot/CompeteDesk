using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Models;
using CompeteDesk.Services.BusinessAnalysis;
using CompeteDesk.ViewModels.Dashboard;

namespace CompeteDesk.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private const string ActiveWorkspaceCookieName = "cd.activeWorkspaceId";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly BusinessAnalysisService _biz;

    public DashboardController(ApplicationDbContext db, UserManager<IdentityUser> userManager, BusinessAnalysisService biz)
    {
        _db = db;
        _userManager = userManager;
        _biz = biz;
    }

    private async Task<string> GetUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id ?? string.Empty;
    }

    // GET: /Dashboard
    // Optional workspaceId lets users switch between workspaces.
    public async Task<IActionResult> Index(int? workspaceId, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        // ------------------------------------------------------------
        // Determine active workspace context
        // Priority: querystring workspaceId -> cookie -> latest
        // ------------------------------------------------------------
        int? activeId = null;

        if (workspaceId.HasValue && workspaceId.Value > 0)
        {
            activeId = workspaceId.Value;
        }
        else if (Request.Cookies.TryGetValue(ActiveWorkspaceCookieName, out var cookieVal)
                 && int.TryParse(cookieVal, out var parsedId)
                 && parsedId > 0)
        {
            activeId = parsedId;
        }

        Workspace? ws = null;

        if (activeId.HasValue)
        {
            ws = await _db.Workspaces
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == activeId.Value && w.OwnerId == userId, ct);
        }

        // Fallback: latest workspace for this user.
        if (ws is null)
        {
            ws = await _db.Workspaces
                .AsNoTracking()
                .Where(w => w.OwnerId == userId)
                .OrderByDescending(w => w.UpdatedAtUtc ?? w.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);

            // If the cookie points to a workspace that no longer exists, clear it.
            if (activeId.HasValue)
            {
                Response.Cookies.Delete(ActiveWorkspaceCookieName);
            }
        }

        // Persist selection when user explicitly switches workspaces.
        if (workspaceId.HasValue && ws is not null)
        {
            Response.Cookies.Append(
                ActiveWorkspaceCookieName,
                ws.Id.ToString(),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(90),
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps
                });
        }

        // IMPORTANT UX:
        // If the user has no workspace yet, do NOT redirect them away from /Dashboard.
        // The Dashboard is the hub for the product; it should still render and
        // simply guide the user to create their first workspace.
        var vm = new DashboardViewModel
        {
            UserDisplayName = User?.Identity?.Name ?? "Strategist",
            StrategyMode = "Growth",
            StrategyScore = 0,
            HealthStatus = "On Track",
            WeeklyFocus = "Pick one high-leverage move and execute it consistently."
        };

        // These tiles are always available; sections below become data-driven when a workspace exists.
        vm.FeatureTiles = BuildFeatureTiles();

        if (ws is null)
        {
            vm.NeedsWorkspace = true;
            vm.WorkspaceId = 0;
            vm.WorkspaceName = "No workspace yet";
            vm.BusinessType = null;
            vm.Country = null;
            vm.NeedsBusinessProfile = false;

            // Show the full feature list, but counts will be 0 until a workspace exists.
            vm.OverviewSummary = new()
            {
                new OverviewSummaryItem { Title = "Strategies", Subtitle = "Playbooks and strategic moves", Count = 0, Badge = "Create a workspace", Href = "/Strategies" },
                new OverviewSummaryItem { Title = "Actions", Subtitle = "Execution and to-dos", Count = 0, Badge = "Create a workspace", Href = "/Actions" },
                new OverviewSummaryItem { Title = "Habits", Subtitle = "Systems & routines", Count = 0, Badge = "Coming soon", Href = "/Habits", Disabled = true },
                new OverviewSummaryItem { Title = "Metrics", Subtitle = "KPIs & tracking", Count = 0, Badge = "Coming soon", Href = "/Metrics", Disabled = true },
                new OverviewSummaryItem { Title = "Website Analysis", Subtitle = "Website insight reports", Count = 0, Badge = "AI", Href = "/WebsiteAnalysis" },
                new OverviewSummaryItem { Title = "War Room", Subtitle = "Intel + plans", Count = 0, Badge = "0 intel • 0 plans", Href = "/WarRoom" },
                new OverviewSummaryItem { Title = "Business Analysis (AI)", Subtitle = "SWOT + Five Forces + competitors", Count = 0, Badge = "Create a workspace", Href = "/BusinessAnalysis" },
            };

            ViewData["Title"] = "Dashboard";
            ViewData["LayoutFluid"] = true;
            ViewData["UseSidebar"] = true;
            return View(vm);
        }

        vm.NeedsWorkspace = false;
        vm.WorkspaceId = ws.Id;
        vm.WorkspaceName = ws.Name;
        vm.BusinessType = ws.BusinessType;
        vm.Country = ws.Country;
        vm.NeedsBusinessProfile = string.IsNullOrWhiteSpace(ws.BusinessType) || string.IsNullOrWhiteSpace(ws.Country);

        // ------------------------------------------------------------
        // Dynamic dashboard sections (no static demo content)
        // ------------------------------------------------------------
        var nowUtc = DateTime.UtcNow;
        var todayUtc = nowUtc.Date;
        var start7 = todayUtc.AddDays(-7);
        var start14 = todayUtc.AddDays(-14);

        // Load core lists once (workspace-scoped)
        var strategies = await _db.Strategies
            .AsNoTracking()
            .Where(s => s.OwnerId == userId && s.WorkspaceId == ws.Id && s.Status == "Active")
            .OrderByDescending(s => s.Priority)
            .ThenByDescending(s => s.UpdatedAtUtc ?? s.CreatedAtUtc)
            .ToListAsync(ct);

        var actions = await _db.Actions
            .AsNoTracking()
            .Where(a => a.OwnerId == userId && a.WorkspaceId == ws.Id)
            .ToListAsync(ct);

        var habits = await _db.Habits
            .AsNoTracking()
            .Where(h => h.OwnerId == userId && h.WorkspaceId == ws.Id && h.IsActive)
            .OrderBy(h => h.Title)
            .ToListAsync(ct);

        var habitIds = habits.Select(h => h.Id).ToList();
        var habitCheckins = habitIds.Count == 0
            ? new List<HabitCheckin>()
            : await _db.HabitCheckins
                .AsNoTracking()
                .Where(c => c.OwnerId == userId && habitIds.Contains(c.HabitId) && c.OccurredOnUtc >= start14)
                .ToListAsync(ct);

        // ------------------------------------------------------------
        // Today’s Critical Actions
        // - Prefer: overdue/today due items, then highest priority open actions
        // ------------------------------------------------------------
        var open = actions.Where(a => !string.Equals(a.Status, "Done", StringComparison.OrdinalIgnoreCase)).ToList();
        var overdueOrToday = open
            .Where(a => a.DueAtUtc.HasValue && a.DueAtUtc.Value.Date <= todayUtc)
            .OrderBy(a => a.DueAtUtc)
            .ThenByDescending(a => a.Priority)
            .Take(5)
            .ToList();

        var fillers = open
            .Except(overdueOrToday)
            .OrderByDescending(a => a.Priority)
            .ThenBy(a => a.DueAtUtc ?? DateTime.MaxValue)
            .Take(Math.Max(0, 5 - overdueOrToday.Count))
            .ToList();

        var todayPick = overdueOrToday.Concat(fillers).ToList();
        vm.TodayActions = todayPick.Select(a =>
        {
            var sName = a.StrategyId.HasValue ? strategies.FirstOrDefault(s => s.Id == a.StrategyId.Value)?.Name : null;
            var principle = a.StrategyId.HasValue ? strategies.FirstOrDefault(s => s.Id == a.StrategyId.Value)?.CorePrinciple : null;

            var impact = (a.Priority >= 2 || (a.DueAtUtc.HasValue && a.DueAtUtc.Value.Date <= todayUtc))
                ? "High"
                : (a.Priority == 1 ? "Medium" : "Low");

            return new TodayActionItem
            {
                Title = a.Title,
                Subtitle = string.IsNullOrWhiteSpace(sName) ? "Action" : sName,
                Principle = string.IsNullOrWhiteSpace(principle) ? (a.SourceBook ?? "Execution") : principle,
                Impact = impact,
                Minutes = EstimateMinutes(a)
            };
        }).ToList();

        // ------------------------------------------------------------
        // Habit Systems (streaks from check-ins)
        // ------------------------------------------------------------
        vm.HabitSystems = habits.Select(h =>
        {
            var streak = ComputeDailyStreak(h, habitCheckins, todayUtc);
            var status = streak >= 7 ? "Stable" : streak >= 3 ? "Building" : "At Risk";

            return new HabitSystemItem
            {
                Habit = h.Title,
                Streak = streak,
                Status = status,
                Cue = "",
                Environment = "",
                Notes = h.Description ?? ""
            };
        }).ToList();

        // ------------------------------------------------------------
        // Active Strategies cards (execution rate derived from actions)
        // ------------------------------------------------------------
        vm.StrategyCards = strategies.Take(6).Select(s =>
        {
            var related = actions.Where(a => a.StrategyId == s.Id && a.CreatedAtUtc >= start14).ToList();
            var totalRel = related.Count;
            var doneRel = related.Count(a => string.Equals(a.Status, "Done", StringComparison.OrdinalIgnoreCase));
            var execRate = totalRel == 0 ? 0 : (int)Math.Round(doneRel * 100.0 / totalRel);
            var eff = execRate >= 70 ? "High" : execRate >= 40 ? "Medium" : "Low";

            return new StrategyCardItem
            {
                Name = s.Name,
                SourceBook = s.SourceBook ?? "Strategy",
                CorePrinciple = s.CorePrinciple ?? "",
                ExecutionRate = execRate,
                Effectiveness = eff
            };
        }).ToList();

        vm.ActiveStrategiesCount = strategies.Count;

        // ------------------------------------------------------------
        // Metrics & Momentum (computed KPIs)
        // ------------------------------------------------------------
        var done7 = actions.Count(a => string.Equals(a.Status, "Done", StringComparison.OrdinalIgnoreCase) && a.UpdatedAtUtc.HasValue && a.UpdatedAtUtc.Value >= start7);
        var donePrev7 = actions.Count(a => string.Equals(a.Status, "Done", StringComparison.OrdinalIgnoreCase)
                                           && a.UpdatedAtUtc.HasValue
                                           && a.UpdatedAtUtc.Value >= start7.AddDays(-7)
                                           && a.UpdatedAtUtc.Value < start7);

        var openCount = open.Count;
        var overdueCount = open.Count(a => a.DueAtUtc.HasValue && a.DueAtUtc.Value.Date < todayUtc);

        var intel7 = await _db.WarIntel
            .AsNoTracking()
            .CountAsync(i => i.OwnerId == userId && i.WorkspaceId == ws.Id && i.CreatedAtUtc >= start7, ct);
        var intelPrev7 = await _db.WarIntel
            .AsNoTracking()
            .CountAsync(i => i.OwnerId == userId && i.WorkspaceId == ws.Id && i.CreatedAtUtc >= start7.AddDays(-7) && i.CreatedAtUtc < start7, ct);

        var habitCompletions7 = habitCheckins.Count(c => c.OccurredOnUtc >= start7);
        var habitExpected7 = habits.Sum(h => (h.Frequency == "Weekly") ? h.TargetCount : h.TargetCount * 7);
        var habitAdherence = habitExpected7 <= 0 ? 0 : (int)Math.Round(Math.Min(1.0, habitCompletions7 / (double)habitExpected7) * 100);

        vm.Kpis = new()
        {
            BuildKpi("Open Actions", openCount.ToString(), TrendFromDelta(openCount, 0), "flat", "Current") ,
            BuildKpi("Done (7d)", done7.ToString(), PercentTrend(donePrev7, done7, out var dir1), dir1, "vs prior 7 days"),
            BuildKpi("Habit Adherence", $"{habitAdherence}%", "", "flat", "Last 7 days"),
            BuildKpi("Intel Signals (7d)", intel7.ToString(), PercentTrend(intelPrev7, intel7, out var dir2), dir2, "vs prior 7 days")
        };

        // Momentum & overall score
        vm.StrategyScore = ComputeStrategyScore(done7, openCount, habitAdherence, intel7);
        vm.HealthStatus = vm.StrategyScore >= 70 ? "On Track" : vm.StrategyScore >= 45 ? "At Risk" : "Off Track";
        vm.StrategyMode = DetermineStrategyMode(strategies.Count, intel7, openCount, overdueCount);
        vm.WeeklyFocus = ComputeWeeklyFocus(todayPick, habits);

        // Weekly review text derived from recent activity
        vm.WeeklyReviewHighlight = BuildWeeklyHighlight(done7, strategies, actions, start7);
        vm.WeeklyReviewFailure = BuildWeeklyFailure(overdueCount, openCount);
        vm.WeeklyReviewAdjustment = BuildWeeklyAdjustment(overdueCount, todayPick, habitAdherence);

        // ------------------------------------------------------------
        // Overview summaries (real counts for the current workspace)
        // ------------------------------------------------------------
        var totalStrategies = strategies.Count;
        var activeStrategies = strategies.Count;
        var totalActions = actions.Count;
        var openActions = openCount;

        var websiteReports = await _db.WebsiteAnalysisReports
            .AsNoTracking()
            .CountAsync(r => r.OwnerId == userId && r.WorkspaceId == ws.Id, ct);

        var warIntelCount = await _db.WarIntel
            .AsNoTracking()
            .CountAsync(i => i.OwnerId == userId && i.WorkspaceId == ws.Id, ct);

        var warPlanCount = await _db.WarPlans
            .AsNoTracking()
            .CountAsync(p => p.OwnerId == userId && p.WorkspaceId == ws.Id, ct);

        var businessReports = await _db.BusinessAnalysisReports
            .AsNoTracking()
            .CountAsync(r => r.OwnerId == userId && r.WorkspaceId == ws.Id, ct);

        // Already set above from strategies list

        // Replace the sample overview with real data
        vm.OverviewSummary = new()
        {
            new OverviewSummaryItem
            {
                Title = "Strategies",
                Subtitle = "Playbooks and strategic moves",
                Count = totalStrategies,
                Badge = activeStrategies > 0 ? $"{activeStrategies} active" : "No active",
                Href = "/Strategies"
            },
            new OverviewSummaryItem
            {
                Title = "Actions",
                Subtitle = "Execution and to-dos",
                Count = totalActions,
                Badge = openActions > 0 ? $"{openActions} open" : "All done",
                Href = "/Actions"
            },
            new OverviewSummaryItem
            {
                Title = "Habits",
                Subtitle = "Systems & routines",
                Count = habits.Count,
                Badge = habits.Count > 0 ? "Active" : "Create one",
                Href = "/Habits",
                Disabled = false
            },
            new OverviewSummaryItem
            {
                Title = "Metrics",
                Subtitle = "KPIs & tracking",
                Count = vm.Kpis.Count,
                Badge = "Auto",
                Href = "/Metrics",
                Disabled = false
            },
            new OverviewSummaryItem
            {
                Title = "Website Analysis",
                Subtitle = "Website insight reports",
                Count = websiteReports,
                Badge = "AI",
                Href = "/WebsiteAnalysis"
            },
            new OverviewSummaryItem
            {
                Title = "War Room",
                Subtitle = "Intel + plans",
                Count = warIntelCount + warPlanCount,
                Badge = $"{warIntelCount} intel • {warPlanCount} plans",
                Href = "/WarRoom"
            },
            new OverviewSummaryItem
            {
                Title = "Business Analysis (AI)",
                Subtitle = "SWOT + Five Forces + competitors",
                Count = businessReports,
                Badge = vm.NeedsBusinessProfile ? "Setup needed" : "Ready",
                Href = "/Dashboard"
            },
        };

        var latest = await _db.BusinessAnalysisReports
            .AsNoTracking()
            .Where(r => r.OwnerId == userId && r.WorkspaceId == ws.Id)
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (latest != null)
        {
            vm.BusinessAnalysis = MapBusinessAnalysis(latest);
        }

        ViewData["Title"] = "Dashboard";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        return View(vm);
    }

    private static List<FeatureTileItem> BuildFeatureTiles()
    {
        return new()
        {
            new FeatureTileItem { Title = "Workspaces", Description = "Create and manage strategic workspaces.", Href = "/Workspaces" },
            new FeatureTileItem { Title = "Strategies", Description = "Build playbooks and strategic moves.", Href = "/Strategies" },
            new FeatureTileItem { Title = "Actions", Description = "Track critical actions and execution.", Href = "/Actions" },
            new FeatureTileItem { Title = "Habits", Description = "Turn strategy into repeatable systems.", Href = "/Habits" },
            new FeatureTileItem { Title = "Metrics", Description = "Measure what matters with KPIs.", Href = "/Metrics" },
            new FeatureTileItem { Title = "Website Analysis", Description = "Analyze a website and generate insights.", Href = "/WebsiteAnalysis" },
            new FeatureTileItem { Title = "War Room", Description = "Capture intel and plans for competition.", Href = "/WarRoom" },
            new FeatureTileItem { Title = "Business Analysis (AI)", Description = "SWOT + Porter’s Five Forces + competitors.", Href = "/BusinessAnalysis" },
            new FeatureTileItem { Title = "AI Strategy Co-Pilot", Description = "Synthesize strategy + intel to draft Blue Ocean hypotheses.", Href = "/StrategyCopilot" }
        };
    }

    private static int EstimateMinutes(ActionItem a)
    {
        // Lightweight heuristic: make it feel dynamic without introducing new schema.
        // Priority: 0=quick, 1=medium, 2+=deep work.
        return a.Priority >= 2 ? 45 : a.Priority == 1 ? 25 : 15;
    }

    private static int ComputeDailyStreak(Habit habit, List<HabitCheckin> checkins, DateTime todayUtc)
    {
        // Daily streak counts consecutive days with at least 1 check-in (for this habit).
        // For weekly habits, we still compute a simple “daily presence” streak for now.
        var set = new HashSet<DateTime>(
            checkins.Where(c => c.HabitId == habit.Id)
                .Select(c => c.OccurredOnUtc.Date));

        var streak = 0;
        var d = todayUtc;
        while (set.Contains(d))
        {
            streak++;
            d = d.AddDays(-1);
        }
        return streak;
    }

    private static MetricKpiItem BuildKpi(string name, string value, string trend, string dir, string sub)
        => new() { Name = name, Value = value, Trend = trend, TrendDirection = dir, Subtext = sub };

    private static string PercentTrend(int previous, int current, out string direction)
    {
        if (previous <= 0 && current > 0)
        {
            direction = "up";
            return "+100%";
        }
        if (previous <= 0 && current <= 0)
        {
            direction = "flat";
            return "Flat";
        }
        var pct = (current - previous) * 100.0 / previous;
        direction = pct > 1 ? "up" : pct < -1 ? "down" : "flat";
        var sign = pct > 0 ? "+" : "";
        return $"{sign}{pct:0}%";
    }

    private static string TrendFromDelta(int current, int delta)
    {
        if (delta == 0) return "";
        return delta > 0 ? $"+{delta}" : delta.ToString();
    }

    private static int ComputeStrategyScore(int done7, int openCount, int habitAdherence, int intel7)
    {
        var execBase = (done7 + openCount) <= 0 ? 0 : (int)Math.Round(done7 * 100.0 / (done7 + openCount));
        var intelScore = Math.Min(100, intel7 * 20);
        var score = (int)Math.Round(execBase * 0.5 + habitAdherence * 0.3 + intelScore * 0.2);
        return Math.Clamp(score, 0, 100);
    }

    private static string DetermineStrategyMode(int activeStrategies, int intel7, int openCount, int overdueCount)
    {
        if (activeStrategies <= 0) return "Stability";
        if (overdueCount > 3) return "Defensive";
        if (intel7 >= 4) return "Offensive";
        if (openCount > 0) return "Growth";
        return "Stability";
    }

    private static string ComputeWeeklyFocus(List<ActionItem> todayPick, List<Habit> habits)
    {
        var top = todayPick.FirstOrDefault();
        if (top != null) return $"Finish: {top.Title}";
        var h = habits.FirstOrDefault();
        if (h != null) return $"Strengthen: {h.Title}";
        return "Pick one high-leverage move and execute it consistently.";
    }

    private static string BuildWeeklyHighlight(int done7, List<Strategy> strategies, List<ActionItem> actions, DateTime start7)
    {
        if (done7 <= 0) return "No completions logged yet — pick 1–2 small wins to restart momentum.";

        // Identify the strategy with the most completed actions in the last 7 days.
        var top = actions
            .Where(a => a.StrategyId.HasValue && string.Equals(a.Status, "Done", StringComparison.OrdinalIgnoreCase)
                        && a.UpdatedAtUtc.HasValue && a.UpdatedAtUtc.Value >= start7)
            .GroupBy(a => a.StrategyId!.Value)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        var topName = top == null ? null : strategies.FirstOrDefault(s => s.Id == top.Key)?.Name;
        return string.IsNullOrWhiteSpace(topName)
            ? $"You completed {done7} action(s) in the last 7 days. Keep the cadence."
            : $"You completed {done7} action(s) in the last 7 days — strongest execution was on “{topName}”.";
    }

    private static string BuildWeeklyFailure(int overdueCount, int openCount)
    {
        if (openCount <= 0) return "Nothing is currently open — great position to plan the next push.";
        if (overdueCount <= 0) return $"No overdue actions — keep your backlog lean (currently {openCount} open).";
        return $"{overdueCount} action(s) are overdue. Clear the oldest 1–2 first to reduce drag.";
    }

    private static string BuildWeeklyAdjustment(int overdueCount, List<ActionItem> todayPick, int habitAdherence)
    {
        if (overdueCount > 0)
            return "Schedule 2 focused blocks this week to clear overdue work. Then keep only 3–5 active actions.";

        if (todayPick.Count > 0)
            return $"Lock in time for “{todayPick[0].Title}”. Small, consistent execution beats big bursts.";

        if (habitAdherence < 50)
            return "Reduce friction: make your key habit easier (2 minutes) and rebuild streak confidence.";

        return "Add one new high-leverage action tied to your top strategy and review progress daily.";
    }

    // POST: /Dashboard/SetBusinessProfile
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetBusinessProfile(int workspaceId, string businessType, string country, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var ws = await _db.Workspaces
            .Where(w => w.Id == workspaceId && w.OwnerId == userId)
            .FirstOrDefaultAsync(ct);

        if (ws == null) return NotFound();

        ws.BusinessType = (businessType ?? string.Empty).Trim();
        ws.Country = (country ?? string.Empty).Trim();
        ws.BusinessProfileUpdatedAtUtc = DateTime.UtcNow;
        ws.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }

    // POST: /Dashboard/GenerateBusinessAnalysis
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateBusinessAnalysis(int workspaceId, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var ws = await _db.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workspaceId && w.OwnerId == userId, ct);

        if (ws == null) return NotFound();
        if (string.IsNullOrWhiteSpace(ws.BusinessType) || string.IsNullOrWhiteSpace(ws.Country))
            return BadRequest(new { ok = false, error = "Missing business profile." });

        var output = await _biz.GenerateAsync(
            new BusinessAnalysisService.GenerateInput(ws.Name, ws.BusinessType!, ws.Country!),
            ct);

        var report = new BusinessAnalysisReport
        {
            WorkspaceId = ws.Id,
            OwnerId = userId,
            BusinessType = ws.BusinessType!,
            Country = ws.Country!,
            AiInsightsJson = string.IsNullOrWhiteSpace(output.Json) ? "{}" : output.Json,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.BusinessAnalysisReports.Add(report);
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }

    private static BusinessAnalysisViewModel MapBusinessAnalysis(BusinessAnalysisReport report)
    {
        // Default
        var vm = new BusinessAnalysisViewModel
        {
            CreatedAtUtc = report.CreatedAtUtc
        };

        try
        {
            var parsed = JsonSerializer.Deserialize<BusinessAnalysisResult>(report.AiInsightsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed == null) return vm;

            vm.Strengths = parsed.Swot?.Strengths ?? new();
            vm.Weaknesses = parsed.Swot?.Weaknesses ?? new();
            vm.Opportunities = parsed.Swot?.Opportunities ?? new();
            vm.Threats = parsed.Swot?.Threats ?? new();

            vm.Rivalry = new ForceVm { Score = parsed.FiveForces?.Rivalry?.Score ?? 0, Notes = parsed.FiveForces?.Rivalry?.Notes };
            vm.NewEntrants = new ForceVm { Score = parsed.FiveForces?.NewEntrants?.Score ?? 0, Notes = parsed.FiveForces?.NewEntrants?.Notes };
            vm.Substitutes = new ForceVm { Score = parsed.FiveForces?.Substitutes?.Score ?? 0, Notes = parsed.FiveForces?.Substitutes?.Notes };
            vm.SupplierPower = new ForceVm { Score = parsed.FiveForces?.SupplierPower?.Score ?? 0, Notes = parsed.FiveForces?.SupplierPower?.Notes };
            vm.BuyerPower = new ForceVm { Score = parsed.FiveForces?.BuyerPower?.Score ?? 0, Notes = parsed.FiveForces?.BuyerPower?.Notes };

            if (parsed.Competitors != null)
            {
                foreach (var c in parsed.Competitors)
                {
                    vm.Competitors.Add(new CompetitorVm
                    {
                        Name = c.Name,
                        WhyRelevant = c.WhyRelevant,
                        Rivalry = new ForceVm { Score = c.FiveForces?.Rivalry?.Score ?? 0, Notes = c.FiveForces?.Rivalry?.Notes },
                        NewEntrants = new ForceVm { Score = c.FiveForces?.NewEntrants?.Score ?? 0, Notes = c.FiveForces?.NewEntrants?.Notes },
                        Substitutes = new ForceVm { Score = c.FiveForces?.Substitutes?.Score ?? 0, Notes = c.FiveForces?.Substitutes?.Notes },
                        SupplierPower = new ForceVm { Score = c.FiveForces?.SupplierPower?.Score ?? 0, Notes = c.FiveForces?.SupplierPower?.Notes },
                        BuyerPower = new ForceVm { Score = c.FiveForces?.BuyerPower?.Score ?? 0, Notes = c.FiveForces?.BuyerPower?.Notes }
                    });
                }
            }
        }
        catch
        {
            // Ignore parse errors.
        }

        return vm;
    }
}
