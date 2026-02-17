using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Services.Recommendations;

namespace CompeteDesk.Controllers;

[Authorize]
public sealed class RecommendationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RecommendationsService _recs;

    public RecommendationsController(ApplicationDbContext db, UserManager<IdentityUser> userManager, RecommendationsService recs)
    {
        _db = db;
        _userManager = userManager;
        _recs = recs;
    }

    private async Task<string?> GetUserIdAsync() => (await _userManager.GetUserAsync(User))?.Id;

    public sealed class IndexVm
    {
        public int? WorkspaceId { get; set; }
        public string? WorkspaceName { get; set; }
        public bool AiEnabled { get; set; }
        public RecommendationsService.Recommendation[] Items { get; set; } = System.Array.Empty<RecommendationsService.Recommendation>();
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? workspaceId, CancellationToken ct)
    {
        ViewData["Title"] = "Smart Recommendations";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var ws = workspaceId.HasValue
            ? await _db.Workspaces.AsNoTracking().FirstOrDefaultAsync(x => x.Id == workspaceId && x.OwnerId == userId, ct)
            : null;

        var items = (await _recs.GetAsync(userId, workspaceId, ct)).Take(8).ToArray();

        return View(new IndexVm
        {
            WorkspaceId = workspaceId,
            WorkspaceName = ws?.Name,
            AiEnabled = _recs.IsAiConfigured,
            Items = items
        });
    }
}
