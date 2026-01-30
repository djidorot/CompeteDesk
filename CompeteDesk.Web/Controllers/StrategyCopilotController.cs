using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Services.StrategyCopilot;
using CompeteDesk.ViewModels.StrategyCopilot;

namespace CompeteDesk.Controllers;

[Authorize]
public sealed class StrategyCopilotController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly StrategyCopilotAiService _ai;
    private readonly UserManager<IdentityUser> _userManager;

    public StrategyCopilotController(
        ApplicationDbContext db,
        StrategyCopilotAiService ai,
        UserManager<IdentityUser> userManager)
    {
        _db = db;
        _ai = ai;
        _userManager = userManager;
    }

    private async Task<string> GetUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id ?? string.Empty;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? workspaceId, CancellationToken ct)
    {
        ViewData["Title"] = "AI Strategy Co-Pilot";
        ViewData["UseSidebar"] = true;
        ViewData["LayoutFluid"] = true;

        var ownerId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(ownerId)) return Unauthorized();

        var workspaces = await _db.Workspaces
            .AsNoTracking()
            .Where(w => w.OwnerId == ownerId)
            .OrderByDescending(w => w.UpdatedAtUtc ?? w.CreatedAtUtc)
            .Select(w => new StrategyCopilotWorkspaceOption
            {
                Id = w.Id,
                Name = w.Name
            })
            .ToListAsync(ct);

        // Default to the most recent workspace.
        if (!workspaceId.HasValue)
            workspaceId = workspaces.FirstOrDefault()?.Id;

        var model = new StrategyCopilotIndexViewModel
        {
            WorkspaceId = workspaceId,
            Workspaces = workspaces
        };

        if (workspaceId.HasValue)
        {
            model.Strategies = await _db.Strategies
                .AsNoTracking()
                .Where(s => s.OwnerId == ownerId && s.WorkspaceId == workspaceId && s.Status == "Active")
                .OrderByDescending(s => s.UpdatedAtUtc ?? s.CreatedAtUtc)
                .Select(s => new StrategyCopilotStrategyOption
                {
                    Id = s.Id,
                    Name = s.Name,
                    Category = s.Category,
                })
                .ToListAsync(ct);

            model.Intel = await _db.WarIntel
                .AsNoTracking()
                .Where(i => i.OwnerId == ownerId && i.WorkspaceId == workspaceId)
                .OrderByDescending(i => i.ObservedAtUtc ?? i.CreatedAtUtc)
                .Select(i => new StrategyCopilotIntelOption
                {
                    Id = i.Id,
                    Title = i.Title,
                    Subject = i.Subject,
                    Confidence = i.Confidence,
                    ObservedAtUtc = i.ObservedAtUtc ?? i.CreatedAtUtc
                })
                .Take(50)
                .ToListAsync(ct);
        }

        return View(model);
    }

    public sealed class GenerateRequest
    {
        public int? WorkspaceId { get; set; }
        public int[] IntelIds { get; set; } = Array.Empty<int>();
        public int[] StrategyIds { get; set; } = Array.Empty<int>();
        public string? StrategyCanvasText { get; set; }
        public string? ErrcEliminate { get; set; }
        public string? ErrcReduce { get; set; }
        public string? ErrcRaise { get; set; }
        public string? ErrcCreate { get; set; }
        public string? Goal { get; set; }
        public string? MarketScope { get; set; }
        public string? Constraints { get; set; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate([FromBody] GenerateRequest req, CancellationToken ct)
    {
        if (!_ai.IsConfigured)
            return BadRequest(new { error = "OpenAI is not configured. Set OpenAI:ApiKey." });

        var ownerId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(ownerId)) return Unauthorized();

        req ??= new GenerateRequest();

        // Enforce ownership of workspace when provided.
        if (req.WorkspaceId.HasValue)
        {
            var ok = await _db.Workspaces.AsNoTracking()
                .AnyAsync(w => w.Id == req.WorkspaceId.Value && w.OwnerId == ownerId, ct);
            if (!ok) return BadRequest(new { error = "Invalid workspace." });
        }

        var doc = await _ai.GenerateAsync(ownerId, new StrategyCopilotAiService.CopilotRequest
        {
            WorkspaceId = req.WorkspaceId,
            IntelIds = (req.IntelIds ?? Array.Empty<int>()).Distinct().ToArray(),
            StrategyIds = (req.StrategyIds ?? Array.Empty<int>()).Distinct().ToArray(),
            StrategyCanvasText = req.StrategyCanvasText,
            ErrcEliminate = req.ErrcEliminate,
            ErrcReduce = req.ErrcReduce,
            ErrcRaise = req.ErrcRaise,
            ErrcCreate = req.ErrcCreate,
            Goal = req.Goal,
            MarketScope = req.MarketScope,
            Constraints = req.Constraints
        }, ct);

        return Content(doc.RootElement.GetRawText(), "application/json");
    }
}
