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
using CompeteDesk.Services;
using CompeteDesk.ViewModels.Dashboard;

namespace CompeteDesk.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private const string ActiveWorkspaceCookieName = ActiveWorkspaceService.ActiveWorkspaceCookieName;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly BusinessAnalysisService _biz;
    private readonly ActiveWorkspaceService _activeWs;

    public DashboardController(ApplicationDbContext db, UserManager<IdentityUser> userManager, BusinessAnalysisService biz, ActiveWorkspaceService activeWs)
    {
        _db = db;
        _userManager = userManager;
        _biz = biz;
        _activeWs = activeWs;
    }

    /// <summary>
    /// Returns the current Dashboard summary cards for the active workspace.
    /// This is used by the client to refresh counts dynamically.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Summary(int? workspaceId, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var resolvedId = await _activeWs.ResolveAsync(HttpContext, userId, workspaceId, ct);
        if (!resolvedId.HasValue)
            return Json(new { workspaceId = 0, items = Array.Empty<object>() });

        var items = await BuildOverviewSummaryAsync(userId, resolvedId.Value, ct);
        return Json(new
        {
            workspaceId = resolvedId.Value,
            items = items.Select(i => new
            {
                title = i.Title,
                subtitle = i.Subtitle,
                count = i.Count,
                badge = i.Badge,
                href = i.Href,
                disabled = i.Disabled
            })
        });
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
            UserDisplayName = User?.Identity?.Name ?? "Candidate",
            StrategyMode = "Prep",
            StrategyScore = 0,
            HealthStatus = "On Track",
            WeeklyFocus = "Pick one high-impact topic and practice it consistently."
        };

        // Populate workspace switcher options (even when no active workspace yet)
        vm.Workspaces = await LoadUserWorkspacesAsync(userId, ct);

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
                new OverviewSummaryItem { Title = "Study Strategies", Subtitle = "Your playbook for topics", Count = 0, Badge = "Create a workspace", Href = "/Strategies" },
                new OverviewSummaryItem { Title = "Daily Actions", Subtitle = "Tasks and practice items", Count = 0, Badge = "Create a workspace", Href = "/Actions" },
                new OverviewSummaryItem { Title = "Study Habits", Subtitle = "Consistency systems", Count = 0, Badge = "Coming soon", Href = "/Habits", Disabled = true },
                new OverviewSummaryItem { Title = "Progress Metrics", Subtitle = "Tracking and reports", Count = 0, Badge = "Coming soon", Href = "/Metrics", Disabled = true },
                new OverviewSummaryItem { Title = "Resource Analysis", Subtitle = "Analyze resources and insights", Count = 0, Badge = "AI", Href = "/WebsiteAnalysis" },
                new OverviewSummaryItem { Title = "War Room", Subtitle = "Insights + plans", Count = 0, Badge = "0 insights • 0 plans", Href = "/WarRoom" },
                new OverviewSummaryItem { Title = "Exam Analysis (AI)", Subtitle = "SWOT + Five Forces + competitors (template)", Count = 0, Badge = "Create a workspace", Href = "/BusinessAnalysis" },
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

        // Back-compat fix:
        // Earlier create flows could save records without WorkspaceId, causing Dashboard summary to stay at 0.
        // If the user only has ONE workspace, safely attach those orphan records to it.
        var wsCount = await _db.Workspaces.AsNoTracking().CountAsync(w => w.OwnerId == userId, ct);
        if (wsCount == 1)
            await AttachOrphanedRecordsToWorkspaceAsync(userId, ws.Id, ct);

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

        // ------------------------------------------------------------
        // Dashboard analytics (real metrics)
        // ------------------------------------------------------------
        vm.AnalyticsCards = new()
        {
            new DashboardAnalyticsCard { Title = "Growth", Value = done7.ToString(), Subtitle = "completed actions in the last 7 days" },
            new DashboardAnalyticsCard { Title = "Performance", Value = $"{habitAdherence}%", Subtitle = "habit adherence over the last 7 days" },
            new DashboardAnalyticsCard { Title = "Risk Level", Value = overdueCount == 0 ? "Low" : overdueCount <= 2 ? "Medium" : "High", Subtitle = overdueCount == 0 ? "no overdue actions" : $"{overdueCount} overdue action(s)" }
        };

        var monthStart = new DateTime(todayUtc.Year, todayUtc.Month, 1).AddMonths(-5);
        vm.MonthlyProgress = Enumerable.Range(0, 6)
            .Select(i => monthStart.AddMonths(i))
            .Select(m => new DashboardChartPoint
            {
                Label = m.ToString("MMM"),
                Value = actions.Count(a => string.Equals(a.Status, "Done", StringComparison.OrdinalIgnoreCase)
                    && a.UpdatedAtUtc.HasValue
                    && a.UpdatedAtUtc.Value.Year == m.Year
                    && a.UpdatedAtUtc.Value.Month == m.Month)
            })
            .ToList();

        vm.CategoryDistribution = strategies
            .GroupBy(s => string.IsNullOrWhiteSpace(s.Category) ? "Uncategorized" : s.Category!)
            .Select(g => new DashboardChartPoint { Label = g.Key, Value = g.Count() })
            .OrderByDescending(x => x.Value)
            .Take(6)
            .ToList();

        if (overdueCount > 0)
            vm.AiSuggestions.Add($"Reduce risk by clearing {overdueCount} overdue action(s) before adding new priorities.");
        if (habitAdherence < 70)
            vm.AiSuggestions.Add("Improve execution rhythm by tightening cues and reducing friction in your habit system.");
        if (strategies.Count > 0 && strategies.All(s => string.IsNullOrWhiteSpace(s.AiSummary)))
            vm.AiSuggestions.Add("Generate strategy suggestions for your top priorities so each strategy has an AI-backed playbook.");
        if (vm.AiSuggestions.Count == 0)
            vm.AiSuggestions.Add("Current signals look healthy. Use AI to compare competitors and strengthen your next growth move.");

        if (vm.BusinessAnalysis?.Competitors?.Count > 0)
        {
            var topCompetitor = vm.BusinessAnalysis.Competitors.First();
            vm.CompetitorSummary = $"Top competitor to watch: {topCompetitor.Name}. {topCompetitor.WhyRelevant}";
        }

        // Workspace switcher options
        vm.Workspaces = await LoadUserWorkspacesAsync(userId, ct);

        ViewData["Title"] = "Dashboard";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        return View(vm);
    }

    private async Task<List<WorkspaceSwitchItem>> LoadUserWorkspacesAsync(string userId, CancellationToken ct)
    {
        return await _db.Workspaces
            .AsNoTracking()
            .Where(w => w.OwnerId == userId)
            .OrderByDescending(w => w.UpdatedAtUtc ?? w.CreatedAtUtc)
            .Select(w => new WorkspaceSwitchItem { Id = w.Id, Name = w.Name })
            .ToListAsync(ct);
    }

    private static List<FeatureTileItem> BuildFeatureTiles()
    {
        return new()
        {
            new FeatureTileItem { Title = "Prep Workspaces", Description = "Create and manage your prep workspace.", Href = "/Workspaces" },
            new FeatureTileItem { Title = "Study Strategies", Description = "Build your playbook for topics and competencies.", Href = "/Strategies" },
            new FeatureTileItem { Title = "Daily Actions", Description = "Track practice tasks and execution.", Href = "/Actions" },
            new FeatureTileItem { Title = "Study Habits", Description = "Turn your plan into repeatable routines.", Href = "/Habits" },
            new FeatureTileItem { Title = "Progress Metrics", Description = "Measure improvement with streaks and reports.", Href = "/Metrics" },
            new FeatureTileItem { Title = "Resource Analysis", Description = "Analyze learning resources and extract insights.", Href = "/WebsiteAnalysis" },
            new FeatureTileItem { Title = "War Room", Description = "Capture insights and plans for weak areas.", Href = "/WarRoom" },
            new FeatureTileItem { Title = "Exam Analysis (AI)", Description = "Templates + AI support for structured analysis.", Href = "/BusinessAnalysis" },
            new FeatureTileItem { Title = "AI Study Co-Pilot", Description = "Get guidance to improve focus and consistency.", Href = "/StrategyCopilot" }
        };
    }

    private async Task<List<OverviewSummaryItem>> BuildOverviewSummaryAsync(string userId, int workspaceId, CancellationToken ct)
    {
        var strategiesActive = await _db.Strategies.AsNoTracking()
            .CountAsync(s => s.OwnerId == userId && s.WorkspaceId == workspaceId && s.Status == "Active", ct);

        var totalActions = await _db.Actions.AsNoTracking()
            .CountAsync(a => a.OwnerId == userId && a.WorkspaceId == workspaceId, ct);

        var openActions = await _db.Actions.AsNoTracking()
            .CountAsync(a => a.OwnerId == userId && a.WorkspaceId == workspaceId && a.Status != "Done", ct);

        var habitsActive = await _db.Habits.AsNoTracking()
            .CountAsync(h => h.OwnerId == userId && h.WorkspaceId == workspaceId && h.IsActive, ct);

        var websiteReports = await _db.WebsiteAnalysisReports.AsNoTracking()
            .CountAsync(r => r.OwnerId == userId && r.WorkspaceId == workspaceId, ct);

        var warIntelCount = await _db.WarIntel.AsNoTracking()
            .CountAsync(i => i.OwnerId == userId && i.WorkspaceId == workspaceId, ct);

        var warPlanCount = await _db.WarPlans.AsNoTracking()
            .CountAsync(p => p.OwnerId == userId && p.WorkspaceId == workspaceId, ct);

        var businessReports = await _db.BusinessAnalysisReports.AsNoTracking()
            .CountAsync(r => r.OwnerId == userId && r.WorkspaceId == workspaceId, ct);

        // NeedsBusinessProfile is evaluated in Index; for summary refresh we keep the badge simple.
        return new()
        {
            new OverviewSummaryItem
            {
                Title = "Strategies",
                Subtitle = "Playbooks and strategic moves",
                Count = strategiesActive,
                Badge = strategiesActive > 0 ? $"{strategiesActive} active" : "No active",
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
                Count = habitsActive,
                Badge = habitsActive > 0 ? "Active" : "Create one",
                Href = "/Habits",
                Disabled = false
            },
            new OverviewSummaryItem
            {
                Title = "Metrics",
                Subtitle = "KPIs & tracking",
                Count = 4,
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
                Badge = "AI",
                Href = "/BusinessAnalysis"
            },
        };
    }

    private async Task AttachOrphanedRecordsToWorkspaceAsync(string userId, int workspaceId, CancellationToken ct)
    {
        // Only safe when the user has exactly one workspace (checked by caller).
        // Attach missing WorkspaceId to prevent Dashboard summary counts from staying at 0.
        var updated = false;

        var orphanStrategies = await _db.Strategies
            .Where(x => x.OwnerId == userId && x.WorkspaceId == null)
            .ToListAsync(ct);
        if (orphanStrategies.Count > 0)
        {
            orphanStrategies.ForEach(x => x.WorkspaceId = workspaceId);
            updated = true;
        }

        var orphanActions = await _db.Actions
            .Where(x => x.OwnerId == userId && x.WorkspaceId == null)
            .ToListAsync(ct);
        if (orphanActions.Count > 0)
        {
            orphanActions.ForEach(x => x.WorkspaceId = workspaceId);
            updated = true;
        }

        var orphanHabits = await _db.Habits
            .Where(x => x.OwnerId == userId && x.WorkspaceId <= 0)
            .ToListAsync(ct);
        if (orphanHabits.Count > 0)
        {
            orphanHabits.ForEach(x => x.WorkspaceId = workspaceId);
            updated = true;
        }

        var orphanIntel = await _db.WarIntel
            .Where(x => x.OwnerId == userId && x.WorkspaceId == null)
            .ToListAsync(ct);
        if (orphanIntel.Count > 0)
        {
            orphanIntel.ForEach(x => x.WorkspaceId = workspaceId);
            updated = true;
        }

        var orphanPlans = await _db.WarPlans
            .Where(x => x.OwnerId == userId && x.WorkspaceId == null)
            .ToListAsync(ct);
        if (orphanPlans.Count > 0)
        {
            orphanPlans.ForEach(x => x.WorkspaceId = workspaceId);
            updated = true;
        }

        var orphanWeb = await _db.WebsiteAnalysisReports
            .Where(x => x.OwnerId == userId && x.WorkspaceId == null)
            .ToListAsync(ct);
        if (orphanWeb.Count > 0)
        {
            orphanWeb.ForEach(x => x.WorkspaceId = workspaceId);
            updated = true;
        }

        var orphanBiz = await _db.BusinessAnalysisReports
            .Where(x => x.OwnerId == userId && x.WorkspaceId <= 0)
            .ToListAsync(ct);
        if (orphanBiz.Count > 0)
        {
            orphanBiz.ForEach(x => x.WorkspaceId = workspaceId);
            updated = true;
        }

        if (updated)
            await _db.SaveChangesAsync(ct);
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
        if (activeStrategies <= 0) return "Baseline";
        if (overdueCount > 3) return "Catch-up";
        if (intel7 >= 4) return "Push";
        if (openCount > 0) return "Intensive";
        return "Baseline";
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

        BusinessAnalysisService.GenerateOutput output;
        try
        {
            output = await _biz.GenerateAsync(
                new BusinessAnalysisService.GenerateInput(ws.Name, ws.BusinessType!, ws.Country!),
                ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // HttpClient timeout surfaces as TaskCanceled/OperationCanceled.
            return StatusCode(504, new
            {
                ok = false,
                error = "AI request timed out. Please try again (or use a simpler business type / country)."
            });
        }
        catch (InvalidOperationException ex)
        {
            // Common case: OpenAI key not configured.
            return BadRequest(new { ok = false, error = ex.Message });
        }
        catch (Exception)
        {
            // Avoid leaking stack traces to the client (AJAX shows raw HTML otherwise).
            return StatusCode(500, new
            {
                ok = false,
                error = "Could not generate analysis right now. Please try again."
            });
        }

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

            vm.Rivalry = new ForceVm { Score = parsed.FiveForces?.Rivalry?.Score ?? 0, Notes = parsed.FiveForces?.Rivalry?.Notes ?? string.Empty };
            vm.NewEntrants = new ForceVm { Score = parsed.FiveForces?.NewEntrants?.Score ?? 0, Notes = parsed.FiveForces?.NewEntrants?.Notes ?? string.Empty };
            vm.Substitutes = new ForceVm { Score = parsed.FiveForces?.Substitutes?.Score ?? 0, Notes = parsed.FiveForces?.Substitutes?.Notes ?? string.Empty };
            vm.SupplierPower = new ForceVm { Score = parsed.FiveForces?.SupplierPower?.Score ?? 0, Notes = parsed.FiveForces?.SupplierPower?.Notes ?? string.Empty };
            vm.BuyerPower = new ForceVm { Score = parsed.FiveForces?.BuyerPower?.Score ?? 0, Notes = parsed.FiveForces?.BuyerPower?.Notes ?? string.Empty };

            if (parsed.Competitors != null)
            {
                foreach (var c in parsed.Competitors)
                {
                    vm.Competitors.Add(new CompetitorVm
                    {
                        Name = c.Name,
                        WhyRelevant = c.WhyRelevant,
                        Rivalry = new ForceVm { Score = c.FiveForces?.Rivalry?.Score ?? 0, Notes = c.FiveForces?.Rivalry?.Notes ?? string.Empty },
                        NewEntrants = new ForceVm { Score = c.FiveForces?.NewEntrants?.Score ?? 0, Notes = c.FiveForces?.NewEntrants?.Notes ?? string.Empty },
                        Substitutes = new ForceVm { Score = c.FiveForces?.Substitutes?.Score ?? 0, Notes = c.FiveForces?.Substitutes?.Notes ?? string.Empty },
                        SupplierPower = new ForceVm { Score = c.FiveForces?.SupplierPower?.Score ?? 0, Notes = c.FiveForces?.SupplierPower?.Notes ?? string.Empty },
                        BuyerPower = new ForceVm { Score = c.FiveForces?.BuyerPower?.Score ?? 0, Notes = c.FiveForces?.BuyerPower?.Notes ?? string.Empty }
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
