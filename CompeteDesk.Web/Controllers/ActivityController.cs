using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Models.Common;
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
    public async Task<IActionResult> Index(
        string? q = null,
        string? actionFilter = null,
        string? entityType = null,
        string? actor = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int page = 1,
        int pageSize = 25,
        bool partial = false,
        CancellationToken ct = default)
    {
        var (userId, isAdmin) = await GetMeAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        // Non-admin sees only own activity. Admin sees system-wide.
        var baseQuery = _db.AuditLogs.AsNoTracking();
        if (!isAdmin)
            baseQuery = baseQuery.Where(x => x.ActorUserId == userId || x.OwnerId == userId);

        // Populate filter option lists (scoped by permissions, not by the selected filter values).
        var actionOptionsTask = baseQuery
            .Select(x => x.Action)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);

        var entityTypeOptionsTask = baseQuery
            .Select(x => x.EntityType)
            .Where(x => x != null && x != "")
            .Distinct()
            .OrderBy(x => x)
            .Select(x => x!)
            .ToListAsync(ct);

        // Apply filters
        var query = baseQuery;

        if (!string.IsNullOrWhiteSpace(actionFilter))
            query = query.Where(x => x.Action == actionFilter);

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(x => x.EntityType == entityType);

        if (!string.IsNullOrWhiteSpace(actor))
            query = query.Where(x => x.ActorEmail != null && EF.Functions.Like(x.ActorEmail, $"%{actor}%"));

        if (fromUtc.HasValue)
            query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);

        if (toUtc.HasValue)
        {
            // Treat date-only inputs as inclusive. If the user provides a date without time,
            // bump to the next day boundary in UTC.
            var to = toUtc.Value;
            if (to.TimeOfDay == TimeSpan.Zero)
                to = to.AddDays(1);
            query = query.Where(x => x.CreatedAtUtc < to);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                (x.Summary != null && EF.Functions.Like(x.Summary, $"%{term}%")) ||
                (x.EntityType != null && EF.Functions.Like(x.EntityType, $"%{term}%")) ||
                (x.EntityId != null && EF.Functions.Like(x.EntityId, $"%{term}%")));
        }

        var projection = query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new ActivityLogItem
            {
                Id = x.Id,
                CreatedAtUtc = x.CreatedAtUtc,
                ActorEmail = x.ActorEmail,
                Action = x.Action,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                Summary = x.Summary
            });

        var paged = await PagedResult<ActivityLogItem>.CreateAsync(projection, page, pageSize, ct);

        var vm = new ActivityLogViewModel
        {
            IsAdmin = isAdmin,
            ScopeLabel = isAdmin ? "System Activity" : "My Activity",
            Q = q,
            ActionFilter = actionFilter,
            EntityTypeFilter = entityType,
            ActorFilter = actor,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            ActionOptions = await actionOptionsTask,
            EntityTypeOptions = await entityTypeOptionsTask,
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            Items = paged.Items.ToList()
        };

        ViewData["Title"] = "Activity Log";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        if (partial || Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_ActivityTable", vm);

        return View(vm);
    }

    // GET: /Activity/History?entityType=Strategy&entityId=123
    public async Task<IActionResult> History(string entityType, string entityId, int page = 1, int pageSize = 25, bool partial = false, CancellationToken ct = default)
    {
        var (userId, isAdmin) = await GetMeAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(entityId))
            return RedirectToAction(nameof(Index));

        var query = _db.EntityChangeHistories.AsNoTracking()
            .Where(x => x.EntityType == entityType && x.EntityId == entityId);

        if (!isAdmin)
            query = query.Where(x => x.OwnerId == userId);

        var projection = query
            .OrderByDescending(x => x.ChangedAtUtc)
            .Select(x => new EntityHistoryItem
            {
                Id = x.Id,
                ChangedAtUtc = x.ChangedAtUtc,
                Action = x.Action,
                ActorEmail = x.ActorEmail,
                BeforeJson = x.BeforeJson,
                AfterJson = x.AfterJson
            });

        var paged = await PagedResult<EntityHistoryItem>.CreateAsync(projection, page, pageSize, ct);

        var vm = new EntityHistoryViewModel
        {
            EntityType = entityType,
            EntityId = entityId,
            Title = $"{entityType} #{entityId} — Change History",
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            Items = paged.Items.ToList()
        };

        ViewData["Title"] = "Change History";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        if (partial || Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_EntityHistoryTable", vm);

        return View(vm);
    }
}
