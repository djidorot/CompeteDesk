// UPDATED FILE: CompeteDesk/Controllers/StrategiesController.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Models;
using CompeteDesk.Services.OpenAI;
using System.Text.Json;
using System.Text.Json.Serialization;
using CompeteDesk.ViewModels.Strategies;

namespace CompeteDesk.Controllers;

[Authorize]
public class StrategiesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly OpenAiChatClient _openAi;

    public StrategiesController(ApplicationDbContext db, UserManager<IdentityUser> userManager, OpenAiChatClient openAi)
    {
        _db = db;
        _userManager = userManager;
        _openAi = openAi;
    }

    private async Task<string> GetUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id ?? string.Empty;
    }

    // GET: /Strategies
    public async Task<IActionResult> Index(string? q, string status = "Active")
    {
        ViewData["Title"] = "Strategies";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var query = _db.Strategies
            .AsNoTracking()
            .Where(x => x.OwnerId == userId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                x.Name.Contains(term) ||
                (x.CorePrinciple != null && x.CorePrinciple.Contains(term)) ||
                (x.Category != null && x.Category.Contains(term)));
        }

        var items = await query
            .OrderByDescending(x => x.Priority)
            .ThenByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
            .ThenBy(x => x.Name)
            .ToListAsync();

        ViewBag.Query = q ?? string.Empty;
        ViewBag.Status = status;

        return View(items);
    }

    // GET: /Strategies/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        ViewData["Title"] = "Strategy Details";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var item = await _db.Strategies
            .AsNoTracking()
            .Include(x => x.Workspace)
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);

        if (item == null) return NotFound();

        // Active strategy count in the same workspace (or across all, if none)
        var activeCountQuery = _db.Strategies.AsNoTracking()
            .Where(x => x.OwnerId == userId && x.Status == "Active");

        if (item.WorkspaceId != null)
        {
            activeCountQuery = activeCountQuery.Where(x => x.WorkspaceId == item.WorkspaceId);
        }

        var activeStrategyCount = await activeCountQuery.CountAsync();

        // Score signals (simple & transparent):
        // - Completeness (0..40)
        // - Execution via action completion (0..60)
        var actionsQuery = _db.Actions.AsNoTracking()
            .Where(a => a.OwnerId == userId && a.StrategyId == item.Id && a.Status != "Archived");

        var totalActions = await actionsQuery.CountAsync();
        var doneActions = await actionsQuery.CountAsync(a => a.Status == "Done");

        var completeness = 0;
        if (!string.IsNullOrWhiteSpace(item.CorePrinciple)) completeness += 10;
        if (!string.IsNullOrWhiteSpace(item.Summary)) completeness += 10;
        if (!string.IsNullOrWhiteSpace(item.StrategyType)) completeness += 10;
        if (totalActions > 0) completeness += 10;

        var execution = 0;
        if (totalActions > 0)
        {
            execution = (int)Math.Round(60.0 * doneActions / totalActions);
        }

        var score = Math.Clamp(completeness + execution, 0, 100);

        var statusKey = score >= 75 ? "OnTrack" : (score >= 45 ? "AtRisk" : "OffTrack");

        var vm = new StrategyDetailsViewModel
        {
            Strategy = item,
            TotalActions = totalActions,
            DoneActions = doneActions,
            Header = new StrategyCommandHeaderViewModel
            {
                WorkspaceName = item.Workspace?.Name ?? "No Workspace",
                StrategyType = string.IsNullOrWhiteSpace(item.StrategyType) ? "Growth" : item.StrategyType,
                ActiveStrategyCount = activeStrategyCount,
                StrategyScore = score,
                StatusKey = statusKey
            }
        };

        return View(vm);
    }

    // GET: /Strategies/Create
    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "New Strategy";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        // Default values
        var model = new Strategy
        {
            SourceBook = null,
            Status = "Active",
            Priority = 0
        };

        // If user has 0 strategies, gently suggest seeding.
        var userId = await GetUserIdAsync();
        ViewBag.HasAny = await _db.Strategies.AnyAsync(x => x.OwnerId == userId);

        return View(model);
    }

    // POST: /Strategies/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Strategy model)
    {
        ViewData["Title"] = "New Strategy";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        model.OwnerId = userId;
        model.CreatedAtUtc = DateTime.UtcNow;
        model.UpdatedAtUtc = DateTime.UtcNow;
        model.Status = string.IsNullOrWhiteSpace(model.Status) ? "Active" : model.Status;
        model.SourceBook = string.IsNullOrWhiteSpace(model.SourceBook) ? null : model.SourceBook;
        model.StrategyType = string.IsNullOrWhiteSpace(model.StrategyType) ? "Growth" : model.StrategyType;

        if (!ModelState.IsValid) return View(model);

        _db.Strategies.Add(model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // GET: /Strategies/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        ViewData["Title"] = "Edit Strategy";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var item = await _db.Strategies.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        return item == null ? NotFound() : View(item);
    }

    // POST: /Strategies/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Strategy model)
    {
        if (id != model.Id) return NotFound();

        ViewData["Title"] = "Edit Strategy";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var item = await _db.Strategies.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        if (item == null) return NotFound();

        if (!ModelState.IsValid) return View(model);

        // Update whitelisted fields
        item.Name = model.Name;
        item.SourceBook = model.SourceBook;
        item.CorePrinciple = model.CorePrinciple;
        item.Summary = model.Summary;
        item.Category = model.Category;
        item.StrategyType = string.IsNullOrWhiteSpace(model.StrategyType) ? item.StrategyType : model.StrategyType;
        item.Status = string.IsNullOrWhiteSpace(model.Status) ? item.Status : model.Status;
        item.Priority = model.Priority;
        item.WorkspaceId = model.WorkspaceId;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // GET: /Strategies/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        ViewData["Title"] = "Delete Strategy";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var item = await _db.Strategies.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);

        return item == null ? NotFound() : View(item);
    }

    // POST: /Strategies/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var item = await _db.Strategies.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        if (item == null) return NotFound();

        _db.Strategies.Remove(item);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // POST: /Strategies/SeedFromBook
    // Inserts the strategy titles (as "Active") if the user has none yet.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SeedFromBook()
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var any = await _db.Strategies.AnyAsync(x => x.OwnerId == userId);
        if (any) return RedirectToAction(nameof(Index));

        var now = DateTime.UtcNow;

        // NOTE:
        // These are short paraphrased titles/labels for seeding (not book passages).
        var seeds = new[]
        {
            // Part I — Self-Directed Warfare
            new Strategy { Name = "Declare War on Your Enemies", Category = "Self-Directed", CorePrinciple = "Identify the real opposition and commit.", Priority = 50 },
            new Strategy { Name = "Do Not Fight the Last War", Category = "Self-Directed", CorePrinciple = "Adapt to the present; avoid repeating patterns.", Priority = 50 },
            new Strategy { Name = "Amidst the Turmoil of Events, Do Not Lose Your Presence of Mind", Category = "Self-Directed", CorePrinciple = "Stay balanced under pressure.", Priority = 50 },
            new Strategy { Name = "Create a Sense of Urgency and Desperation", Category = "Self-Directed", CorePrinciple = "Use constraints to force action.", Priority = 50 },

            // Part II — Organizational (Team) Warfare
            new Strategy { Name = "Avoid the Snares of Groupthink", Category = "Organizational", CorePrinciple = "Lead clearly without herd irrationality.", Priority = 40 },
            new Strategy { Name = "Segment Your Forces", Category = "Organizational", CorePrinciple = "Small autonomous units move faster.", Priority = 40 },
            new Strategy { Name = "Transform Your War into a Crusade", Category = "Organizational", CorePrinciple = "Morale rises when mission is shared.", Priority = 40 },

            // Part III — Defensive Warfare
            new Strategy { Name = "Pick Your Battles Carefully", Category = "Defensive", CorePrinciple = "Choose conflicts with favorable economics.", Priority = 30 },
            new Strategy { Name = "Turn the Tables", Category = "Defensive", CorePrinciple = "Let them move first; counter from advantage.", Priority = 30 },
            new Strategy { Name = "Create a Threatening Presence", Category = "Defensive", CorePrinciple = "Deterrence through reputation and uncertainty.", Priority = 30 },
            new Strategy { Name = "Trade Space for Time", Category = "Defensive", CorePrinciple = "Nonengagement buys time to reposition.", Priority = 30 },

            // Part IV — Offensive Warfare
            new Strategy { Name = "Lose Battles but Win the War", Category = "Offensive", CorePrinciple = "Optimize for the ultimate objective.", Priority = 35 },
            new Strategy { Name = "Know Your Enemy", Category = "Offensive", CorePrinciple = "Intelligence: read motives and signals.", Priority = 35 },
            new Strategy { Name = "Overwhelm Resistance with Speed and Suddenness", Category = "Offensive", CorePrinciple = "Speed creates imbalance and error.", Priority = 35 },
            new Strategy { Name = "Control the Dynamic", Category = "Offensive", CorePrinciple = "Define the relationship’s terms.", Priority = 25 },
            new Strategy { Name = "Hit Them Where It Hurts", Category = "Offensive", CorePrinciple = "Attack the center of gravity.", Priority = 25 },
            new Strategy { Name = "Defeat Them in Detail", Category = "Offensive", CorePrinciple = "Split the problem; win sequentially.", Priority = 25 },
            new Strategy { Name = "Expose and Attack Your Opponent's Soft Flank", Category = "Offensive", CorePrinciple = "Avoid head-on resistance; attack the side.", Priority = 25 },
            new Strategy { Name = "Envelop the Enemy", Category = "Offensive", CorePrinciple = "Apply pressure from all sides.", Priority = 25 },
            new Strategy { Name = "Maneuver Them into Weakness", Category = "Offensive", CorePrinciple = "Position them so victory is easy.", Priority = 25 },
            new Strategy { Name = "Negotiate While Advancing", Category = "Offensive", CorePrinciple = "Talk while creating relentless leverage.", Priority = 25 },
            new Strategy { Name = "Know How to End Things", Category = "Offensive", CorePrinciple = "Choose exits; end cleanly.", Priority = 25 },

            // Part V — Unconventional Warfare
            new Strategy { Name = "Weave a Seamless Blend of Fact and Fiction", Category = "Unconventional", CorePrinciple = "Control perceptions; manufacture reality.", Priority = 20 },
            new Strategy { Name = "Take the Line of Least Expectation", Category = "Unconventional", CorePrinciple = "Upset patterns; strike where unexpected.", Priority = 20 },
            new Strategy { Name = "Occupy the Moral High Ground", Category = "Unconventional", CorePrinciple = "Frame your cause as more just.", Priority = 20 },
            new Strategy { Name = "Deny Them Targets", Category = "Unconventional", CorePrinciple = "Be elusive; offer no clean target.", Priority = 20 },
            new Strategy { Name = "Seem to Work for the Interests of Others While Furthering Your Own", Category = "Unconventional", CorePrinciple = "Alliances: let others do the work.", Priority = 20 },
            new Strategy { Name = "Give Your Rivals Enough Rope to Hang Themselves", Category = "Unconventional", CorePrinciple = "Make them self-destruct.", Priority = 20 },
            new Strategy { Name = "Take Small Bites", Category = "Unconventional", CorePrinciple = "Accumulate quietly; avoid sharp grabs.", Priority = 20 },
            new Strategy { Name = "Penetrate Their Minds", Category = "Unconventional", CorePrinciple = "Communication as psychological warfare.", Priority = 20 },
            new Strategy { Name = "Destroy from Within", Category = "Unconventional", CorePrinciple = "Infiltrate and turn the inner front.", Priority = 20 },
            new Strategy { Name = "Dominate While Seeming to Submit", Category = "Unconventional", CorePrinciple = "Hidden aggression; control through compliance.", Priority = 20 },
            new Strategy { Name = "Sow Uncertainty and Panic Through Acts of Terror", Category = "Unconventional", CorePrinciple = "Trigger overreaction; keep your balance.", Priority = 20 }
        };

        foreach (var s in seeds)
        {
            s.OwnerId = userId;
            s.SourceBook = null;
            s.Status = "Active";
            s.CreatedAtUtc = now;
            s.UpdatedAtUtc = now;
        }

        _db.Strategies.AddRange(seeds);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------
    // AI: Generate a competitive playbook for a strategy
    // ------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateAiPlaybook(int id, [FromBody] StrategyAiRequest req, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var strategy = await _db.Strategies.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId, ct);
        if (strategy == null) return NotFound();

        // Minimal baseline if OpenAI isn't configured.
        if (!_openAi.IsConfigured)
        {
            var fallback = new
            {
                oneLineSummary = "OpenAI not configured. Set OpenAI:ApiKey to enable AI playbooks.",
                strategicAim = req.Objective ?? "",
                battlefield = new { market = req.MarketOrArena ?? "", enemy = req.Competitor ?? "", theirLikelyMove = "", ourEdge = "" },
                principleFit = new { whyThisStrategy = "", whenNotToUse = "" },
                executionPlan = Array.Empty<object>(),
                counterMoves = Array.Empty<object>(),
                quickWins = new[] { "Add OpenAI API key (OpenAI:ApiKey)." },
                risks = Array.Empty<object>(),
                kpis = Array.Empty<object>(),
                recommendedActions = new[]
                {
                    new { title = "Configure OpenAI", description = "Set OpenAI:ApiKey in appsettings or user-secrets.", priority = 5, dueDays = 1, category = "Setup" }
                }
            };

            var json = JsonSerializer.Serialize(fallback);
            strategy.AiInsightsJson = json;
            strategy.AiSummary = fallback.oneLineSummary;
            strategy.AiUpdatedAtUtc = DateTime.UtcNow;
            strategy.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return Json(new { ok = true, aiJson = json, summary = strategy.AiSummary, updatedAtUtc = strategy.AiUpdatedAtUtc });
        }

        var workspace = strategy.WorkspaceId != null
            ? await _db.Workspaces.AsNoTracking().FirstOrDefaultAsync(w => w.Id == strategy.WorkspaceId && w.OwnerId == userId, ct)
            : null;

        // Pull a small amount of context to keep the prompt grounded and cheap.
        var recentIntel = await _db.WarIntel.AsNoTracking()
            .Where(x => x.OwnerId == userId && x.WorkspaceId == strategy.WorkspaceId)
            .OrderByDescending(x => x.ObservedAtUtc ?? x.CreatedAtUtc)
            .Select(x => new { x.Title, x.Subject, x.Signal, x.Confidence, x.Tags })
            .Take(6)
            .ToListAsync(ct);

        var activePlans = await _db.WarPlans.AsNoTracking()
            .Where(x => x.OwnerId == userId && x.WorkspaceId == strategy.WorkspaceId && x.Status != "Archived")
            .OrderByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
            .Select(x => new { x.Name, x.Objective, x.Approach, x.Status })
            .Take(3)
            .ToListAsync(ct);

        var existingActions = await _db.Actions.AsNoTracking()
            .Where(x => x.OwnerId == userId && x.StrategyId == strategy.Id && x.Status != "Archived")
            .OrderByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
            .Select(x => new { x.Title, x.Status, x.Priority, x.DueAtUtc })
            .Take(8)
            .ToListAsync(ct);

        var payload = new
        {
            strategy = new
            {
                strategy.Id,
                strategy.Name,
                strategy.Category,
                strategy.CorePrinciple,
                strategy.Summary,
                strategy.Priority,
                strategy.SourceBook
            },
            workspace = workspace == null ? null : new { workspace.Id, workspace.Name, workspace.Description },
            userInput = new
            {
                req.MarketOrArena,
                req.Objective,
                req.Competitor,
                req.OurPosition,
                req.Constraints,
                req.TimeHorizon,
                req.EthicalLine,
                req.SuccessDefinition
            },
            signals = new { recentIntel, activePlans, existingActions }
        };

        // IMPORTANT FIX:
        // Use a C# raw string literal so we don't break compilation with backslashes/quotes/newlines.
        var systemPrompt = """
You are a product strategist and competitive analyst.
Your job: convert a named strategy into a practical competitive playbook.

Important constraints:
- Base conclusions only on the provided JSON context. If something is missing, ask for it inside the output.
- Do NOT quote copyrighted text or reproduce book passages.
- Keep language professional and business-safe (no illegal or deceptive instructions).

Return STRICT JSON with this schema:
{
  "oneLineSummary": "...",
  "strategicAim": "...",
  "battlefield": { "market": "...", "enemy": "...", "theirLikelyMove": "...", "ourEdge": "..." },
  "principleFit": { "whyThisStrategy": "...", "whenNotToUse": "...", "assumptionsToValidate": ["..."] },
  "executionPlan": [
    { "step": 1, "title": "...", "detail": "...", "ownerRole": "...", "timeframe": "...", "successMetric": "..." }
  ],
  "counterMoves": [
    { "enemyMove": "...", "ourResponse": "...", "signalToWatch": "..." }
  ],
  "quickWins": ["..."],
  "risks": [ { "risk": "...", "mitigation": "...", "severity": "High|Medium|Low" } ],
  "kpis": [ { "name": "...", "target": "...", "why": "..." } ],
  "recommendedActions": [
    { "title": "...", "description": "...", "priority": 1, "dueDays": 7, "category": "..." }
  ],
  "questions": ["..."]
}

Rules:
- Keep each string concise (generally < 140 characters), except "detail" which can be ~300.
- recommendedActions should be actionable tasks that can be created as Action Items.
- If the strategy is not attached to a workspace, suggest which workspace context would help.
""";

        var userJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        string aiJson;
        try
        {
            aiJson = await _openAi.CreateJsonInsightsAsync(systemPrompt, userJson, ct);
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, error = $"AI call failed: {ex.Message}" });
        }

        // Store the raw JSON + a short summary.
        strategy.AiInsightsJson = aiJson;
        strategy.AiSummary = TryExtractOneLineSummary(aiJson) ?? "AI playbook generated.";
        strategy.AiUpdatedAtUtc = DateTime.UtcNow;
        strategy.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Json(new { ok = true, aiJson, summary = strategy.AiSummary, updatedAtUtc = strategy.AiUpdatedAtUtc });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateActionsFromAi(int id, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var strategy = await _db.Strategies.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId, ct);
        if (strategy == null) return NotFound();

        if (string.IsNullOrWhiteSpace(strategy.AiInsightsJson))
            return BadRequest(new { ok = false, error = "No AI insights found. Generate a playbook first." });

        StrategyAiPlaybook? playbook;
        try
        {
            playbook = JsonSerializer.Deserialize<StrategyAiPlaybook>(strategy.AiInsightsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return BadRequest(new { ok = false, error = "AI JSON could not be parsed." });
        }

        if (playbook?.RecommendedActions == null || playbook.RecommendedActions.Count == 0)
            return BadRequest(new { ok = false, error = "AI did not return any recommended actions." });

        var now = DateTime.UtcNow;
        var created = 0;

        foreach (var a in playbook.RecommendedActions.Take(20))
        {
            if (string.IsNullOrWhiteSpace(a.Title)) continue;

            var due = a.DueDays != null
                ? now.AddDays(Math.Clamp(a.DueDays.Value, 1, 365))
                : (DateTime?)null;

            _db.Actions.Add(new ActionItem
            {
                OwnerId = userId,
                WorkspaceId = strategy.WorkspaceId,
                StrategyId = strategy.Id,
                Title = a.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(a.Description) ? null : a.Description.Trim(),
                Category = string.IsNullOrWhiteSpace(a.Category) ? "AI" : a.Category.Trim(),
                Status = "Planned",
                Priority = Math.Clamp(a.Priority ?? 3, 1, 5) * 10,
                DueAtUtc = due,
                SourceBook = strategy.SourceBook,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            created++;
        }

        if (created == 0)
            return BadRequest(new { ok = false, error = "No valid action items were found in AI output." });

        await _db.SaveChangesAsync(ct);
        return Json(new { ok = true, created });
    }

    private static string? TryExtractOneLineSummary(string aiJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(aiJson);
            if (doc.RootElement.TryGetProperty("oneLineSummary", out var s) && s.ValueKind == JsonValueKind.String)
                return s.GetString();
        }
        catch
        {
            // ignore
        }
        return null;
    }

    public sealed class StrategyAiRequest
    {
        public string? MarketOrArena { get; set; }
        public string? Objective { get; set; }
        public string? Competitor { get; set; }
        public string? OurPosition { get; set; }
        public string? Constraints { get; set; }
        public string? TimeHorizon { get; set; }
        public string? EthicalLine { get; set; }
        public string? SuccessDefinition { get; set; }
    }

    public sealed class StrategyAiPlaybook
    {
        [JsonPropertyName("recommendedActions")]
        public List<StrategyAiAction> RecommendedActions { get; set; } = new();
    }

    public sealed class StrategyAiAction
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("priority")]
        public int? Priority { get; set; }

        [JsonPropertyName("dueDays")]
        public int? DueDays { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }
    }
}
