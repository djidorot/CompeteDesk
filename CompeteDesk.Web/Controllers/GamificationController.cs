using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Services.Gamification;

namespace CompeteDesk.Controllers;

[Authorize]
public sealed class GamificationController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly GamificationService _gamification;

    public GamificationController(ApplicationDbContext db, UserManager<IdentityUser> userManager, GamificationService gamification)
    {
        _db = db;
        _userManager = userManager;
        _gamification = gamification;
    }

    private async Task<string?> GetUserIdAsync() => (await _userManager.GetUserAsync(User))?.Id;

    public sealed class IndexVm
    {
        public Models.Gamification.UserGamificationProfile? Profile { get; set; }
        public Models.Gamification.UserBadge[] Badges { get; set; } = Array.Empty<Models.Gamification.UserBadge>();
        public Models.Gamification.XpEvent[] RecentXp { get; set; } = Array.Empty<Models.Gamification.XpEvent>();
        public int XpToNextLevel { get; set; }
        public int NextLevel { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Gamification";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var profile = await _gamification.GetOrCreateAsync(userId, ct);

        var badges = await _db.UserBadges.AsNoTracking()
            .Where(b => b.OwnerId == userId)
            .OrderByDescending(b => b.EarnedAtUtc)
            .Take(24)
            .ToArrayAsync(ct);

        var xp = await _db.XpEvents.AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(20)
            .ToArrayAsync(ct);

        var nextLevel = GamificationService.ComputeLevel(profile.TotalXp) + 1;
        var nextThreshold = (nextLevel - 1) * 100;
        var toNext = Math.Max(0, nextThreshold - profile.TotalXp);

        return View(new IndexVm
        {
            Profile = profile,
            Badges = badges,
            RecentXp = xp,
            NextLevel = nextLevel,
            XpToNextLevel = toNext
        });
    }
}
