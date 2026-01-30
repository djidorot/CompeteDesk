using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompeteDesk.Data;
using CompeteDesk.ViewModels.AiHistory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompeteDesk.Controllers;

[Authorize]
public sealed class AiHistoryController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public AiHistoryController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // GET: /AiHistory?workspaceId=1&feature=WarRoom.RedTeamPlan&q=pricing
    public async Task<IActionResult> Index(int? workspaceId, string? feature, string? q, CancellationToken ct)
    {
        ViewData["Title"] = "AI History";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        // FIX: GetUserIdAsync expects IdentityUser, not ClaimsPrincipal.
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var query = _db.DecisionTraces.AsNoTracking()
            .Where(x => x.OwnerId == userId);

        if (workspaceId.HasValue)
            query = query.Where(x => x.WorkspaceId == workspaceId);

        if (!string.IsNullOrWhiteSpace(feature))
            query = query.Where(x => x.Feature == feature);

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(x =>
                (x.EntityTitle != null && x.EntityTitle.Contains(q)) ||
                (x.Feature != null && x.Feature.Contains(q)) ||
                (x.EntityType != null && x.EntityType.Contains(q)));
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .Select(x => new AiHistoryRow
            {
                Id = x.Id,
                CreatedAtUtc = x.CreatedAtUtc,
                Feature = x.Feature,
                WorkspaceId = x.WorkspaceId,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                EntityTitle = x.EntityTitle
            })
            .ToListAsync(ct);

        var vm = new AiHistoryIndexViewModel
        {
            WorkspaceId = workspaceId,
            Feature = feature,
            Q = q,
            Items = items
        };

        return View(vm);
    }

    // GET: /AiHistory/Details/5
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        ViewData["Title"] = "AI Trace";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var item = await _db.DecisionTraces.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId, ct);

        if (item == null) return NotFound();

        return View(item);
    }
}
