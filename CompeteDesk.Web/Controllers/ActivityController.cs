using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.ViewModels.Activity;

namespace CompeteDesk.Controllers;

[Authorize]
public class ActivityController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public ActivityController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private async Task<(string userId, bool isAdmin)> GetMeAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        var userId = user?.Id ?? string.Empty;
        var isAdmin = user is not null && await _userManager.IsInRoleAsync(user, IdentitySeeder.AdminRoleName);
        return (userId, isAdmin);
    }

    // GET: /Activity
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var (userId, isAdmin) = await GetMeAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        // Non-admin sees only own activity. Admin sees system-wide.
        var query = _db.AuditLogs.AsNoTracking();
        if (!isAdmin)
            query = query.Where(x => x.ActorUserId == userId || x.OwnerId == userId);

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .Select(x => new ActivityLogItem
            {
                Id = x.Id,
                CreatedAtUtc = x.CreatedAtUtc,
                ActorEmail = x.ActorEmail,
                Action = x.Action,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                Summary = x.Summary
            })
            .ToListAsync(ct);

        var vm = new ActivityLogViewModel
        {
            IsAdmin = isAdmin,
            ScopeLabel = isAdmin ? "System Activity" : "My Activity",
            Items = items
        };

        ViewData["Title"] = "Activity Log";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        return View(vm);
    }

    // GET: /Activity/History?entityType=Strategy&entityId=123
    public async Task<IActionResult> History(string entityType, string entityId, CancellationToken ct)
    {
        var (userId, isAdmin) = await GetMeAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(entityId))
            return RedirectToAction(nameof(Index));

        var query = _db.EntityChangeHistories.AsNoTracking()
            .Where(x => x.EntityType == entityType && x.EntityId == entityId);

        if (!isAdmin)
            query = query.Where(x => x.OwnerId == userId);

        var items = await query
            .OrderByDescending(x => x.ChangedAtUtc)
            .Take(200)
            .Select(x => new EntityHistoryItem
            {
                Id = x.Id,
                ChangedAtUtc = x.ChangedAtUtc,
                Action = x.Action,
                ActorEmail = x.ActorEmail,
                BeforeJson = x.BeforeJson,
                AfterJson = x.AfterJson
            })
            .ToListAsync(ct);

        var vm = new EntityHistoryViewModel
        {
            EntityType = entityType,
            EntityId = entityId,
            Title = $"{entityType} #{entityId} — Change History",
            Items = items
        };

        ViewData["Title"] = "Change History";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        return View(vm);
    }
}
