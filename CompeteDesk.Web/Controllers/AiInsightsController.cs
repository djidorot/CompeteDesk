using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.ViewModels.AiInsights;

namespace CompeteDesk.Controllers;

[Authorize]
public sealed class AiInsightsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public AiInsightsController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? workspaceId, CancellationToken ct)
    {
        ViewData["Title"] = "AI Insights";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var nowUtc = DateTime.UtcNow;
        var start7 = nowUtc.AddDays(-7);
        var start30 = nowUtc.AddDays(-30);
        var start14 = nowUtc.AddDays(-14);

        // Workspace scope (optional)
        int? wsId = null;
        string? wsName = null;

        if (workspaceId.HasValue)
        {
            var ws = await _db.Workspaces.AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == workspaceId.Value && w.OwnerId == userId, ct);
            if (ws != null)
            {
                wsId = ws.Id;
                wsName = ws.Name;
            }
        }

        var tracesQ = _db.DecisionTraces.AsNoTracking().Where(t => t.OwnerId == userId);
        if (wsId.HasValue) tracesQ = tracesQ.Where(t => t.WorkspaceId == wsId.Value);

        var tracesLast7 = await tracesQ.CountAsync(t => t.CreatedAtUtc >= start7, ct);
        var tracesLast30 = await tracesQ.CountAsync(t => t.CreatedAtUtc >= start30, ct);
        var lastTraceAt = await tracesQ.OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => (DateTime?)t.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        var strategiesQ = _db.Strategies.AsNoTracking().Where(s => s.OwnerId == userId && s.Status == "Active");
        if (wsId.HasValue) strategiesQ = strategiesQ.Where(s => s.WorkspaceId == wsId.Value);

        var strategiesTotal = await strategiesQ.CountAsync(ct);
        var strategiesWithPlaybook = await strategiesQ.CountAsync(s => s.AiUpdatedAtUtc != null || s.AiInsightsJson != null, ct);

        var actionsQ = _db.Actions.AsNoTracking().Where(a => a.OwnerId == userId);
        if (wsId.HasValue) actionsQ = actionsQ.Where(a => a.WorkspaceId == wsId.Value);

        var actionsOpen = await actionsQ.CountAsync(a => !string.Equals(a.Status, "Done", StringComparison.OrdinalIgnoreCase), ct);
        var actionsDone14 = await actionsQ.CountAsync(a => string.Equals(a.Status, "Done", StringComparison.OrdinalIgnoreCase)
                                                          && a.UpdatedAtUtc != null
                                                          && a.UpdatedAtUtc.Value >= start14, ct);

        // Feature usage
        var featureUsage = await tracesQ
            .Where(t => t.CreatedAtUtc >= start30)
            .GroupBy(t => t.Feature)
            .Select(g => new AiFeatureUsageRow { Feature = g.Key, Count30Days = g.Count() })
            .OrderByDescending(x => x.Count30Days)
            .Take(12)
            .ToListAsync(ct);

        var vm = new AiInsightsViewModel
        {
            WorkspaceId = wsId,
            WorkspaceName = wsName,
            Performance = new AiPerformanceBlock
            {
                TracesLast7Days = tracesLast7,
                TracesLast30Days = tracesLast30,
                LastTraceAtUtc = lastTraceAt,
                StrategiesTotal = strategiesTotal,
                StrategiesWithPlaybook = strategiesWithPlaybook,
                ActionsOpen = actionsOpen,
                ActionsDoneLast14Days = actionsDone14
            },
            FeatureUsage = featureUsage
        };

        // ------------------------------------------------------------
        // Weak Area Detection (heuristic, transparent)
        // ------------------------------------------------------------
        if (strategiesTotal > 0)
        {
            var noAiCount = strategiesTotal - strategiesWithPlaybook;
            if (noAiCount > 0)
            {
                vm.WeakAreas.Add(new AiWeakAreaItem
                {
                    Title = "Strategies missing AI playbooks",
                    Detail = $"{noAiCount} strategy(ies) have no AI Competitive Playbook yet.",
                    Href = "/Strategies"
                });
            }

            var highPriorityNoActions = await _db.Strategies.AsNoTracking()
                .Where(s => s.OwnerId == userId
                            && (!wsId.HasValue || s.WorkspaceId == wsId.Value)
                            && s.Status == "Active"
                            && s.Priority >= 2)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    actionCount = _db.Actions.Count(a => a.OwnerId == userId && a.StrategyId == s.Id && (!wsId.HasValue || a.WorkspaceId == wsId.Value))
                })
                .Where(x => x.actionCount == 0)
                .OrderBy(x => x.Name)
                .Take(5)
                .ToListAsync(ct);

            foreach (var item in highPriorityNoActions)
            {
                vm.WeakAreas.Add(new AiWeakAreaItem
                {
                    Title = "High-priority strategy without execution",
                    Detail = $"\"{item.Name}\" has priority ≥ 2 but no Action Items yet.",
                    Href = $"/Strategies/Details/{item.Id}#ai"
                });
            }
        }

        if (actionsOpen >= 10)
        {
            vm.WeakAreas.Add(new AiWeakAreaItem
            {
                Title = "Execution backlog building",
                Detail = $"You have {actionsOpen} open actions. Consider generating a focused playbook and pruning low-impact tasks.",
                Href = "/Actions"
            });
        }

        // Intel confidence check
        var lowConfidenceIntel = await _db.WarIntel.AsNoTracking()
            .Where(i => i.OwnerId == userId
                        && (!wsId.HasValue || i.WorkspaceId == wsId.Value)
                        // Confidence is stored as 1-5 (low to high)
                        && i.Confidence <= 2)
            .CountAsync(ct);

        if (lowConfidenceIntel > 0)
        {
            vm.WeakAreas.Add(new AiWeakAreaItem
            {
                Title = "Low-confidence intel",
				// Confidence is stored as 1-5 (low to high). ≤2 means low-confidence.
				Detail = $"{lowConfidenceIntel} intel item(s) have low confidence (≤ 2). Validate signals before committing resources.",
                Href = "/WarRoom"
            });
        }

        // ------------------------------------------------------------
        // Recommended Focus Areas (actionable next steps)
        // ------------------------------------------------------------
        if (strategiesTotal == 0)
        {
            vm.Recommendations.Add(new AiRecommendationItem
            {
                Title = "Create your first strategy",
                Detail = "Add at least one Strategy so AI can generate playbooks and execution plans.",
                Href = "/Strategies/Create"
            });
        }
        else
        {
            vm.Recommendations.Add(new AiRecommendationItem
            {
                Title = "Run AI Assist on a strategy",
                Detail = "Open a Strategy → AI Assist → generate SWOT + Improvements, then convert the best items into Action Items.",
                Href = "/Strategies"
            });
        }

        if (wsId.HasValue)
        {
            vm.Recommendations.Add(new AiRecommendationItem
            {
                Title = "Use Strategy Co-Pilot",
                Detail = "Combine your Intel + Strategies to generate a unified plan with counters and KPIs.",
                Href = $"/StrategyCopilot?workspaceId={wsId.Value}"
            });
        }
        else
        {
            vm.Recommendations.Add(new AiRecommendationItem
            {
                Title = "Pick a workspace context",
                Detail = "AI insights are stronger when tied to a Workspace. Create/select a Workspace to unlock scoped recommendations.",
                Href = "/Workspaces"
            });
        }

        if (actionsDone14 == 0)
        {
            vm.Recommendations.Add(new AiRecommendationItem
            {
                Title = "Close one loop this week",
                Detail = "Aim to complete at least one Action Item in the next 7 days to build momentum and improve your signal-to-noise ratio.",
                Href = "/Actions"
            });
        }

        return View(vm);
    }
}
