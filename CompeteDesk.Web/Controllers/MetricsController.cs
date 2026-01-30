using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompeteDesk.Data;
using CompeteDesk.Models;
using CompeteDesk.ViewModels.Metrics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompeteDesk.Controllers;

[Authorize]
public sealed class MetricsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public MetricsController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // GET: /Metrics
    public async Task<IActionResult> Index(
        string? tab,
        string? range,
        string? from,
        string? to,
        CancellationToken ct)
    {
        // Render with the same shell as /Dashboard (left panel)
        ViewData["UseSidebar"] = true;
        ViewData["LayoutFluid"] = true;

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var selectedTab = string.IsNullOrWhiteSpace(tab) ? "overview" : tab.Trim().ToLowerInvariant();
        var selectedRange = string.IsNullOrWhiteSpace(range) ? "day" : range.Trim().ToLowerInvariant();

        // Time window (UTC)
        var utcNow = DateTime.UtcNow;
        DateTime endUtc = utcNow;
        DateTime startUtc = utcNow.AddDays(-1);

        // If from/to are provided, honor them (YYYY-MM-DD). "to" is inclusive in UI, exclusive in queries.
        if (TryParseDateOnly(from, out var fromDate))
        {
            startUtc = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            selectedRange = "custom";
        }
        if (TryParseDateOnly(to, out var toDate))
        {
            endUtc = toDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1);
            selectedRange = "custom";
        }

        if (selectedRange != "custom")
        {
            switch (selectedRange)
            {
                case "week":
                    startUtc = utcNow.AddDays(-7);
                    break;
                case "month":
                    startUtc = utcNow.AddDays(-30);
                    break;
                case "day":
                default:
                    selectedRange = "day";
                    startUtc = utcNow.AddDays(-1);
                    break;
            }
        }

        var window = endUtc - startUtc;
        var priorStartUtc = startUtc - window;
        var priorEndUtc = startUtc;

        // -------------------------
        // Metrics & Momentum (Key Metrics)
        // -------------------------
        if (selectedTab == "momentum")
        {
            var momentumVm = await BuildMomentumViewModelAsync(userId, selectedTab, selectedRange, startUtc, endUtc, priorStartUtc, priorEndUtc, ct);
            // Render with the same view (Index.cshtml) - it conditionally switches on SelectedTab.
            return View(momentumVm);
        }

        // -------------------------
        // Totals (match Dashboard "Overview" concepts)
        // -------------------------
        var totalWorkspaces = await _db.Workspaces.AsNoTracking().CountAsync(w => w.OwnerId == userId, ct);
        var totalStrategies = await _db.Strategies.AsNoTracking().CountAsync(s => s.OwnerId == userId, ct);
        var totalActions = await _db.ActionItems.AsNoTracking().CountAsync(a => a.OwnerId == userId, ct);
        var totalHabits = await _db.Habits.AsNoTracking().CountAsync(h => h.OwnerId == userId, ct);
        var totalWarIntel = await _db.WarIntel.AsNoTracking().CountAsync(i => i.OwnerId == userId, ct);
        var totalWarPlans = await _db.WarPlans.AsNoTracking().CountAsync(p => p.OwnerId == userId, ct);
        var totalWebReports = await _db.WebsiteAnalysisReports.AsNoTracking().CountAsync(r => r.OwnerId == userId, ct);
        var totalBizReports = await _db.BusinessAnalysisReports.AsNoTracking().CountAsync(r => r.OwnerId == userId, ct);
        var totalAiTraces = await _db.DecisionTraces.AsNoTracking().CountAsync(t => t.OwnerId == userId, ct);

        // -------------------------
        // New items in selected range vs prior
        // -------------------------
        async Task<int> CountInRangeAsync<T>(IQueryable<T> q, Func<T, DateTime> createdAt)
        {
            // Not used - left for clarity (we do per-entity queries below)
            await Task.CompletedTask;
            return 0;
        }

        var newWorkspaces = await _db.Workspaces.AsNoTracking()
            .CountAsync(w => w.OwnerId == userId && w.CreatedAtUtc >= startUtc && w.CreatedAtUtc < endUtc, ct);
        var priorWorkspaces = await _db.Workspaces.AsNoTracking()
            .CountAsync(w => w.OwnerId == userId && w.CreatedAtUtc >= priorStartUtc && w.CreatedAtUtc < priorEndUtc, ct);

        var newStrategies = await _db.Strategies.AsNoTracking()
            .CountAsync(s => s.OwnerId == userId && s.CreatedAtUtc >= startUtc && s.CreatedAtUtc < endUtc, ct);
        var priorStrategies = await _db.Strategies.AsNoTracking()
            .CountAsync(s => s.OwnerId == userId && s.CreatedAtUtc >= priorStartUtc && s.CreatedAtUtc < priorEndUtc, ct);

        var newActions = await _db.ActionItems.AsNoTracking()
            .CountAsync(a => a.OwnerId == userId && a.CreatedAtUtc >= startUtc && a.CreatedAtUtc < endUtc, ct);
        var priorActions = await _db.ActionItems.AsNoTracking()
            .CountAsync(a => a.OwnerId == userId && a.CreatedAtUtc >= priorStartUtc && a.CreatedAtUtc < priorEndUtc, ct);

        var newAiTraces = await _db.DecisionTraces.AsNoTracking()
            .CountAsync(t => t.OwnerId == userId && t.CreatedAtUtc >= startUtc && t.CreatedAtUtc < endUtc, ct);
        var priorAiTraces = await _db.DecisionTraces.AsNoTracking()
            .CountAsync(t => t.OwnerId == userId && t.CreatedAtUtc >= priorStartUtc && t.CreatedAtUtc < priorEndUtc, ct);

        // -------------------------
        // Buckets for charts
        // -------------------------
        var bucket = GetBucketSpec(startUtc, endUtc, selectedRange);
        var labels = new List<string>(bucket.Count);
        var ranges = new List<(DateTime Start, DateTime End)>(bucket.Count);

        for (int i = 0; i < bucket.Count; i++)
        {
            var bStart = startUtc.Add(bucket.Step * i);
            var bEnd = (i == bucket.Count - 1) ? endUtc : startUtc.Add(bucket.Step * (i + 1));
            ranges.Add((bStart, bEnd));
            labels.Add(bucket.Label(bStart));
        }

        // Fetch timestamps once per entity type (small volumes expected for a single user).
        async Task<List<DateTime>> FetchCreatedAsync<TEntity>(IQueryable<TEntity> set, Func<TEntity, string> owner, Func<TEntity, DateTime> created)
        {
            // We can't pass Funcs into SQL; fetch minimal rows via projection per entity type below.
            await Task.CompletedTask;
            return new List<DateTime>();
        }

        // Projection queries (SQL-friendly)
        var wsCreated = await _db.Workspaces.AsNoTracking()
            .Where(x => x.OwnerId == userId && x.CreatedAtUtc >= startUtc && x.CreatedAtUtc < endUtc)
            .Select(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var stratCreated = await _db.Strategies.AsNoTracking()
            .Where(x => x.OwnerId == userId && x.CreatedAtUtc >= startUtc && x.CreatedAtUtc < endUtc)
            .Select(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var actionCreated = await _db.ActionItems.AsNoTracking()
            .Where(x => x.OwnerId == userId && x.CreatedAtUtc >= startUtc && x.CreatedAtUtc < endUtc)
            .Select(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var habitCreated = await _db.Habits.AsNoTracking()
            .Where(x => x.OwnerId == userId && x.CreatedAtUtc >= startUtc && x.CreatedAtUtc < endUtc)
            .Select(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var intelCreated = await _db.WarIntel.AsNoTracking()
            .Where(x => x.OwnerId == userId && x.CreatedAtUtc >= startUtc && x.CreatedAtUtc < endUtc)
            .Select(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var planCreated = await _db.WarPlans.AsNoTracking()
            .Where(x => x.OwnerId == userId && x.CreatedAtUtc >= startUtc && x.CreatedAtUtc < endUtc)
            .Select(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var webRepCreated = await _db.WebsiteAnalysisReports.AsNoTracking()
            .Where(x => x.OwnerId == userId && x.CreatedAtUtc >= startUtc && x.CreatedAtUtc < endUtc)
            .Select(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var bizRepCreated = await _db.BusinessAnalysisReports.AsNoTracking()
            .Where(x => x.OwnerId == userId && x.CreatedAtUtc >= startUtc && x.CreatedAtUtc < endUtc)
            .Select(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var aiCreated = await _db.DecisionTraces.AsNoTracking()
            .Where(x => x.OwnerId == userId && x.CreatedAtUtc >= startUtc && x.CreatedAtUtc < endUtc)
            .Select(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        // Helper: count timestamps in a bucket
        static int CountBucket(List<DateTime> ts, DateTime bStart, DateTime bEnd)
            => ts.Count(t => t >= bStart && t < bEnd);

        // Chart points depend on tab
        List<int> leftPoints = new();
        List<int> rightPoints = new();
        string leftTitle;
        string rightTitle;
        bool leftPercent = false;
        bool rightPercent = false;

        for (int i = 0; i < ranges.Count; i++)
        {
            var (bStart, bEnd) = ranges[i];

            int activity = 0;
            activity += CountBucket(wsCreated, bStart, bEnd);
            activity += CountBucket(stratCreated, bStart, bEnd);
            activity += CountBucket(actionCreated, bStart, bEnd);
            activity += CountBucket(habitCreated, bStart, bEnd);
            activity += CountBucket(intelCreated, bStart, bEnd);
            activity += CountBucket(planCreated, bStart, bEnd);
            activity += CountBucket(webRepCreated, bStart, bEnd);
            activity += CountBucket(bizRepCreated, bStart, bEnd);
            // AI traces are tracked separately (right chart)
            int aiRuns = CountBucket(aiCreated, bStart, bEnd);

            switch (selectedTab)
            {
                case "workspaces":
                    leftPoints.Add(CountBucket(wsCreated, bStart, bEnd));
                    rightPoints.Add(activity);
                    break;
                case "strategies":
                    leftPoints.Add(CountBucket(stratCreated, bStart, bEnd));
                    rightPoints.Add(CountBucket(actionCreated, bStart, bEnd));
                    break;
                case "actions":
                    leftPoints.Add(CountBucket(actionCreated, bStart, bEnd));
                    rightPoints.Add(activity);
                    break;
                case "warroom":
                    leftPoints.Add(CountBucket(intelCreated, bStart, bEnd));
                    rightPoints.Add(CountBucket(planCreated, bStart, bEnd));
                    break;
                case "ai":
                    leftPoints.Add(aiRuns);
                    rightPoints.Add(activity);
                    break;
                case "overview":
                default:
                    leftPoints.Add(activity);
                    rightPoints.Add(aiRuns);
                    break;
            }
        }

        switch (selectedTab)
        {
            case "workspaces":
                leftTitle = "Workspaces created";
                rightTitle = "All activity (new items)";
                break;
            case "strategies":
                leftTitle = "Strategies created";
                rightTitle = "Actions created";
                break;
            case "actions":
                leftTitle = "Actions created";
                rightTitle = "All activity (new items)";
                break;
            case "warroom":
                leftTitle = "War Intel created";
                rightTitle = "War Plans created";
                break;
            case "ai":
                leftTitle = "AI runs (Decision Traces)";
                rightTitle = "All activity (new items)";
                break;
            case "overview":
            default:
                leftTitle = "Activity over time (new items)";
                rightTitle = "AI runs over time";
                break;
        }

        // -------------------------
        // Tables
        // -------------------------
        // Left table: top modules by new items (current vs prior)
        var moduleNow = new Dictionary<string, int>
        {
            ["Workspaces"] = wsCreated.Count,
            ["Strategies"] = stratCreated.Count,
            ["Actions"] = actionCreated.Count,
            ["Habits"] = habitCreated.Count,
            ["War Intel"] = intelCreated.Count,
            ["War Plans"] = planCreated.Count,
            ["Website Reports"] = webRepCreated.Count,
            ["Business Reports"] = bizRepCreated.Count,
            ["AI Runs"] = aiCreated.Count
        };

        // Prior for module table (only need counts, not series)
        async Task<int> PriorCountAsync<TEntity>(IQueryable<TEntity> set, Func<TEntity, string> ownerSelector, Func<TEntity, DateTime> createdSelector)
        {
            await Task.CompletedTask;
            return 0;
        }

        var modulePrior = new Dictionary<string, int>
        {
            ["Workspaces"] = await _db.Workspaces.AsNoTracking().CountAsync(x => x.OwnerId == userId && x.CreatedAtUtc >= priorStartUtc && x.CreatedAtUtc < priorEndUtc, ct),
            ["Strategies"] = await _db.Strategies.AsNoTracking().CountAsync(x => x.OwnerId == userId && x.CreatedAtUtc >= priorStartUtc && x.CreatedAtUtc < priorEndUtc, ct),
            ["Actions"] = await _db.ActionItems.AsNoTracking().CountAsync(x => x.OwnerId == userId && x.CreatedAtUtc >= priorStartUtc && x.CreatedAtUtc < priorEndUtc, ct),
            ["Habits"] = await _db.Habits.AsNoTracking().CountAsync(x => x.OwnerId == userId && x.CreatedAtUtc >= priorStartUtc && x.CreatedAtUtc < priorEndUtc, ct),
            ["War Intel"] = await _db.WarIntel.AsNoTracking().CountAsync(x => x.OwnerId == userId && x.CreatedAtUtc >= priorStartUtc && x.CreatedAtUtc < priorEndUtc, ct),
            ["War Plans"] = await _db.WarPlans.AsNoTracking().CountAsync(x => x.OwnerId == userId && x.CreatedAtUtc >= priorStartUtc && x.CreatedAtUtc < priorEndUtc, ct),
            ["Website Reports"] = await _db.WebsiteAnalysisReports.AsNoTracking().CountAsync(x => x.OwnerId == userId && x.CreatedAtUtc >= priorStartUtc && x.CreatedAtUtc < priorEndUtc, ct),
            ["Business Reports"] = await _db.BusinessAnalysisReports.AsNoTracking().CountAsync(x => x.OwnerId == userId && x.CreatedAtUtc >= priorStartUtc && x.CreatedAtUtc < priorEndUtc, ct),
            ["AI Runs"] = await _db.DecisionTraces.AsNoTracking().CountAsync(x => x.OwnerId == userId && x.CreatedAtUtc >= priorStartUtc && x.CreatedAtUtc < priorEndUtc, ct),
        };

        var leftTable = moduleNow
            .Select(kvp =>
            {
                var prior = modulePrior.TryGetValue(kvp.Key, out var p) ? p : 0;
                return new MetricsRankRow
                {
                    Name = kvp.Key,
                    ValueText = kvp.Value.ToString("N0", CultureInfo.InvariantCulture),
                    ChangePct = PctDelta(kvp.Value, prior)
                };
            })
            .OrderByDescending(r => SafeInt(r.ValueText))
            .Take(6)
            .ToList();

        // Right table: Actions by status (overall)
        var statusRows = await _db.ActionItems.AsNoTracking()
            .Where(a => a.OwnerId == userId)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var statusNow = statusRows.ToDictionary(x => x.Status ?? "Unknown", x => x.Count);

        // Prior period: status counts of NEW actions only (so change isn't weird)
        var statusPriorRows = await _db.ActionItems.AsNoTracking()
            .Where(a => a.OwnerId == userId && a.CreatedAtUtc >= priorStartUtc && a.CreatedAtUtc < priorEndUtc)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var statusPrior = statusPriorRows.ToDictionary(x => x.Status ?? "Unknown", x => x.Count);

        var rightTable = statusNow
            .Select(kvp =>
            {
                var prior = statusPrior.TryGetValue(kvp.Key, out var p) ? p : 0;
                return new MetricsRankRow
                {
                    Name = string.IsNullOrWhiteSpace(kvp.Key) ? "Unspecified" : kvp.Key,
                    ValueText = kvp.Value.ToString("N0", CultureInfo.InvariantCulture),
                    ChangePct = PctDelta(kvp.Value, prior)
                };
            })
            .OrderByDescending(r => SafeInt(r.ValueText))
            .Take(6)
            .ToList();

        // -------------------------
        // Build VM
        // -------------------------
        var vm = new MetricsViewModel
        {
            SelectedTab = selectedTab,
            SelectedRange = selectedRange,
            FromDate = startUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ToDate = endUtc.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),

            Kpis = new List<MetricsKpiCard>
            {
                new MetricsKpiCard
                {
                    Icon = "□",
                    Label = "Workspaces",
                    ValueText = totalWorkspaces.ToString("N0", CultureInfo.InvariantCulture),
                    DeltaPct = PctDelta(newWorkspaces, priorWorkspaces)
                },
                new MetricsKpiCard
                {
                    Icon = "▦",
                    Label = "Strategies",
                    ValueText = totalStrategies.ToString("N0", CultureInfo.InvariantCulture),
                    DeltaPct = PctDelta(newStrategies, priorStrategies)
                },
                new MetricsKpiCard
                {
                    Icon = "✓",
                    Label = "Actions",
                    ValueText = totalActions.ToString("N0", CultureInfo.InvariantCulture),
                    DeltaPct = PctDelta(newActions, priorActions)
                },
                new MetricsKpiCard
                {
                    Icon = "✦",
                    Label = "AI runs",
                    ValueText = totalAiTraces.ToString("N0", CultureInfo.InvariantCulture),
                    DeltaPct = PctDelta(newAiTraces, priorAiTraces)
                }
            },

            LeftChartTitle = leftTitle,
            LeftChartPoints = leftPoints,
            LeftChartLabels = labels,
            LeftChartPercent = leftPercent,

            RightChartTitle = rightTitle,
            RightChartPoints = rightPoints,
            RightChartLabels = labels,
            RightChartPercent = rightPercent,

            LeftTableTitle = selectedTab == "actions" ? "Top modules (new items)" : "Top modules (new items)",
            RightTableTitle = "Actions by status",
            LeftTableRows = leftTable,
            RightTableRows = rightTable
        };

        return View(vm);
    }

    // -------------------------
    // Metrics & Momentum (Key Metrics)
    // -------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveKeyMetricsConfig(string? range, string? from, string? to, CancellationToken ct)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var enabledIds = Request.Form["enabledIds"].ToArray()
            .Select(s => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .Where(n => n > 0)
            .ToHashSet();

        var defs = await _db.KeyMetricDefinitions
            .Where(d => d.OwnerId == userId)
            .ToListAsync(ct);

        foreach (var d in defs)
        {
            d.IsEnabled = enabledIds.Contains(d.Id);

            var orderKey = $"order_{d.Id}";
            var nameKey = $"name_{d.Id}";
            if (int.TryParse(Request.Form[orderKey], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sort))
                d.SortOrder = sort;

            var newName = (Request.Form[nameKey].ToString() ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(newName) && newName.Length <= 80)
                d.DisplayName = newName;

            d.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        return RedirectToAction("Index", new { tab = "momentum", range, from, to });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddKeyMetricEntry(int definitionId, string? date, decimal value, string? range, string? from, string? to, CancellationToken ct)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var def = await _db.KeyMetricDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == definitionId && d.OwnerId == userId, ct);

        if (def == null) return NotFound();

        // Store per-day at midnight UTC.
        DateTime dateUtc = DateTime.UtcNow.Date;
        if (!string.IsNullOrWhiteSpace(date)
            && DateTime.TryParseExact(date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            dateUtc = parsed.ToUniversalTime().Date;
        }

        var existing = await _db.KeyMetricEntries
            .FirstOrDefaultAsync(e => e.OwnerId == userId && e.DefinitionId == definitionId && e.DateUtc == dateUtc, ct);

        if (existing == null)
        {
            _db.KeyMetricEntries.Add(new KeyMetricEntry
            {
                OwnerId = userId,
                DefinitionId = definitionId,
                DateUtc = dateUtc,
                Value = value,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        return RedirectToAction("Index", new { tab = "momentum", range, from, to });
    }

    private async Task<MetricsViewModel> BuildMomentumViewModelAsync(
        string userId,
        string selectedTab,
        string selectedRange,
        DateTime startUtc,
        DateTime endUtc,
        DateTime priorStartUtc,
        DateTime priorEndUtc,
        CancellationToken ct)
    {
        // Ensure default definitions exist for the user.
        var hasAny = await _db.KeyMetricDefinitions.AsNoTracking().AnyAsync(d => d.OwnerId == userId, ct);
        if (!hasAny)
        {
            _db.KeyMetricDefinitions.AddRange(new[]
            {
                new KeyMetricDefinition { OwnerId = userId, Key = "Revenue", DisplayName = "Revenue", Unit = "currency", IsEnabled = true, SortOrder = 10, CreatedAtUtc = DateTime.UtcNow },
                new KeyMetricDefinition { OwnerId = userId, Key = "Leads", DisplayName = "Leads", Unit = "number", IsEnabled = true, SortOrder = 20, CreatedAtUtc = DateTime.UtcNow },
                new KeyMetricDefinition { OwnerId = userId, Key = "ConversionRate", DisplayName = "Conversion Rate", Unit = "percent", IsEnabled = true, SortOrder = 30, CreatedAtUtc = DateTime.UtcNow },
                new KeyMetricDefinition { OwnerId = userId, Key = "Engagement", DisplayName = "Engagement", Unit = "number", IsEnabled = true, SortOrder = 40, CreatedAtUtc = DateTime.UtcNow },
                new KeyMetricDefinition { OwnerId = userId, Key = "OutputCount", DisplayName = "Output Count", Unit = "number", IsEnabled = true, SortOrder = 50, CreatedAtUtc = DateTime.UtcNow },
            });
            await _db.SaveChangesAsync(ct);
        }

        var allDefs = await _db.KeyMetricDefinitions.AsNoTracking()
            .Where(d => d.OwnerId == userId)
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.DisplayName)
            .ToListAsync(ct);

        var enabledDefs = allDefs.Where(d => d.IsEnabled).ToList();

        // Buckets (same logic as activity charts)
        var bucket = GetBucketSpec(startUtc, endUtc, selectedRange);
        var labels = new List<string>(bucket.Count);
        var ranges = new List<(DateTime Start, DateTime End)>(bucket.Count);
        for (int i = 0; i < bucket.Count; i++)
        {
            var bStart = startUtc.Add(bucket.Step * i);
            var bEnd = (i == bucket.Count - 1) ? endUtc : startUtc.Add(bucket.Step * (i + 1));
            ranges.Add((bStart, bEnd));
            labels.Add(bucket.Label(bStart));
        }

        // Pull all entries for enabled metrics in a single query
        var enabledIds = enabledDefs.Select(d => d.Id).ToList();
        var entries = await _db.KeyMetricEntries.AsNoTracking()
            .Where(e => e.OwnerId == userId && enabledIds.Contains(e.DefinitionId) && e.DateUtc >= startUtc && e.DateUtc < endUtc)
            .Select(e => new { e.DefinitionId, e.DateUtc, e.Value })
            .ToListAsync(ct);

        // Latest values for delta comparisons
        var latestByDef = await _db.KeyMetricEntries.AsNoTracking()
            .Where(e => e.OwnerId == userId && enabledIds.Contains(e.DefinitionId) && e.DateUtc < endUtc)
            .GroupBy(e => e.DefinitionId)
            .Select(g => g.OrderByDescending(x => x.DateUtc).Select(x => new { x.DefinitionId, x.Value }).FirstOrDefault())
            .ToListAsync(ct);

        var priorLatestByDef = await _db.KeyMetricEntries.AsNoTracking()
            .Where(e => e.OwnerId == userId && enabledIds.Contains(e.DefinitionId) && e.DateUtc >= priorStartUtc && e.DateUtc < priorEndUtc)
            .GroupBy(e => e.DefinitionId)
            .Select(g => g.OrderByDescending(x => x.DateUtc).Select(x => new { x.DefinitionId, x.Value }).FirstOrDefault())
            .ToListAsync(ct);

        var latestMap = latestByDef.Where(x => x != null).ToDictionary(x => x!.DefinitionId, x => x!.Value);
        var priorLatestMap = priorLatestByDef.Where(x => x != null).ToDictionary(x => x!.DefinitionId, x => x!.Value);

        // Helper: percent delta for decimals
        static double PctDeltaDec(decimal current, decimal prior)
        {
            if (prior <= 0m) return current > 0m ? 100d : 0d;
            return (double)((current - prior) * 100m / prior);
        }

        static string SignalOf(double delta)
        {
            if (Math.Abs(delta) < 0.1) return "flat";
            return delta > 0 ? "up" : "down";
        }

        string FormatValue(string unit, decimal v)
        {
            if (unit == "percent") return $"{v:0.#}%";
            if (unit == "currency")
            {
                // Prefer PHP formatting when available (Asia/Manila default), fallback to invariant.
                try
                {
                    var ph = CultureInfo.GetCultureInfo("en-PH");
                    return string.Format(ph, "{0:C0}", v);
                }
                catch
                {
                    return v.ToString("N0", CultureInfo.InvariantCulture);
                }
            }
            return v.ToString("N0", CultureInfo.InvariantCulture);
        }

        var cards = new List<KeyMetricCardViewModel>();

        foreach (var def in enabledDefs)
        {
            var defEntries = entries.Where(e => e.DefinitionId == def.Id).OrderBy(e => e.DateUtc).ToList();

            // Build bucket series; use last known value within each bucket, carry-forward from previous.
            var series = new List<decimal>(ranges.Count);
            decimal last = 0m;
            for (int i = 0; i < ranges.Count; i++)
            {
                var (bStart, bEnd) = ranges[i];
                var inBucket = defEntries.Where(e => e.DateUtc >= bStart && e.DateUtc < bEnd).OrderBy(e => e.DateUtc).ToList();
                if (inBucket.Count > 0)
                    last = inBucket[^1].Value;
                series.Add(last);
            }

            var current = latestMap.TryGetValue(def.Id, out var c) ? c : 0m;
            var prior = priorLatestMap.TryGetValue(def.Id, out var p) ? p : 0m;
            var deltaPct = PctDeltaDec(current, prior);

            cards.Add(new KeyMetricCardViewModel
            {
                DefinitionId = def.Id,
                Key = def.Key,
                DisplayName = string.IsNullOrWhiteSpace(def.DisplayName) ? def.Key : def.DisplayName,
                Unit = def.Unit,
                ValueText = FormatValue(def.Unit, current),
                DeltaPct = deltaPct,
                Points = series,
                Labels = labels,
                Percent = def.Unit == "percent",
                Signal = SignalOf(deltaPct)
            });
        }

        var vm = new MetricsViewModel
        {
            SelectedTab = selectedTab,
            SelectedRange = selectedRange,
            FromDate = startUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ToDate = endUtc.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),

            KeyMetrics = cards,
            KeyMetricConfig = allDefs.Select(d => new KeyMetricConfigRowViewModel
            {
                Id = d.Id,
                Key = d.Key,
                DisplayName = d.DisplayName,
                Unit = d.Unit,
                IsEnabled = d.IsEnabled,
                SortOrder = d.SortOrder
            }).ToList()
        };

        return vm;
    }

    // -------------------------
    // Helpers
    // -------------------------
    private static double PctDelta(int current, int prior)
    {
        if (prior <= 0) return current > 0 ? 100d : 0d;
        return (current - prior) * 100d / prior;
    }

    private static int SafeInt(string text)
    {
        if (int.TryParse(text.Replace(",", "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            return n;
        return 0;
    }

    private static bool TryParseDateOnly(string? input, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(input)) return false;
        return DateOnly.TryParseExact(input.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private sealed record BucketSpec(TimeSpan Step, int Count, Func<DateTime, string> Label);

    private static BucketSpec GetBucketSpec(DateTime startUtc, DateTime endUtc, string selectedRange)
    {
        // Keep the UI readable (not too many ticks)
        var total = endUtc - startUtc;

        if (selectedRange == "day" || total.TotalDays <= 2)
        {
            // 8 buckets (3 hours each)
            return new BucketSpec(TimeSpan.FromHours(3), 8, dt => dt.ToString("htt", CultureInfo.InvariantCulture).ToLowerInvariant());
        }

        if (selectedRange == "week" || total.TotalDays <= 10)
        {
            // Daily buckets
            var days = (int)Math.Ceiling(total.TotalDays);
            return new BucketSpec(TimeSpan.FromDays(1), Math.Max(1, days), dt => dt.ToString("MMM d", CultureInfo.InvariantCulture));
        }

        // Month/custom longer: 10 buckets
        var count = 10;
        var step = TimeSpan.FromTicks(total.Ticks / count);
        return new BucketSpec(step, count, dt => dt.ToString("MMM d", CultureInfo.InvariantCulture));
    }

}
