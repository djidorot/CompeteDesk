using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using CompeteDesk.Models.Common;
using CompeteDesk.Services.Gamification;

namespace CompeteDesk.Controllers;

[Authorize]
public class ActionsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IMemoryCache _cache;
    private readonly GamificationService _gamification;

    public ActionsController(ApplicationDbContext db, UserManager<IdentityUser> userManager, IMemoryCache cache, GamificationService gamification)
    {
        _db = db;
        _userManager = userManager;
        _cache = cache;
        _gamification = gamification;
    }

    private async Task<string> GetUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id ?? string.Empty;
    }

    // GET: /Actions
    public async Task<IActionResult> Index(string? q, string status = "Planned", int? workspaceId = null, int? strategyId = null, string sort = "due", int page = 1, int pageSize = 25, bool partial = false, CancellationToken ct = default)
    {
        ViewData["Title"] = "Actions";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var query = _db.Actions
            .AsNoTracking()
            .Where(x => x.OwnerId == userId);

        // Filter dropdown data (cached briefly per user)
        var workspaces = await _cache.GetOrCreateAsync($"ws:list:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(45);
            return await _db.Workspaces
                .AsNoTracking()
                .Where(w => w.OwnerId == userId)
                .OrderBy(w => w.Name)
                .Select(w => new { w.Id, w.Name })
                .ToListAsync(ct);
        }) ?? new();
        ViewBag.Workspaces = new SelectList(workspaces, "Id", "Name", workspaceId);

        var strategiesCacheKey = $"st:list:{userId}:ws:{workspaceId?.ToString() ?? "all"}";
        var strategies = await _cache.GetOrCreateAsync(strategiesCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(45);
            return await _db.Strategies
                .AsNoTracking()
                .Where(s => s.OwnerId == userId && (workspaceId == null || s.WorkspaceId == workspaceId))
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name })
                .ToListAsync(ct);
        }) ?? new();
        ViewBag.Strategies = new SelectList(strategies, "Id", "Name", strategyId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (workspaceId != null)
        {
            query = query.Where(x => x.WorkspaceId == workspaceId);
        }

        if (strategyId != null)
        {
            query = query.Where(x => x.StrategyId == strategyId);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                x.Title.Contains(term) ||
                (x.Description != null && x.Description.Contains(term)) ||
                (x.Category != null && x.Category.Contains(term)));
        }

        query = sort switch
        {
            "newest" => query.OrderByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc),
            "alpha" => query.OrderBy(x => x.Title),
            "priority" => query.OrderByDescending(x => x.Priority)
                .ThenBy(x => x.DueAtUtc ?? DateTime.MaxValue)
                .ThenByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
                .ThenBy(x => x.Title),
            // default: due date first (nulls last)
            _ => query.OrderBy(x => x.DueAtUtc ?? DateTime.MaxValue)
                .ThenByDescending(x => x.Priority)
                .ThenByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
                .ThenBy(x => x.Title)
        };

        var paged = await PagedResult<ActionItem>.CreateAsync(query, page, pageSize, ct);

        ViewBag.Page = paged.Page;
        ViewBag.PageSize = paged.PageSize;
        ViewBag.TotalCount = paged.TotalCount;
        ViewBag.TotalPages = paged.TotalPages;

        ViewBag.Query = q ?? string.Empty;
        ViewBag.Status = status;
        ViewBag.WorkspaceId = workspaceId;
        ViewBag.StrategyId = strategyId;
        ViewBag.Sort = sort;

        if (partial || Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_ActionsList", paged.Items.ToList());

        return View(paged.Items.ToList());
    }

    // GET: /Actions/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        ViewData["Title"] = "Action Details";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var item = await _db.Actions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);

        return item == null ? NotFound() : View(item);
    }

    // GET: /Actions/Create
    [Authorize(Policy = "CanEdit")]
    public IActionResult Create()
    {
        ViewData["Title"] = "New Action";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var model = new ActionItem
        {
            SourceBook = null,
            Status = "Planned",
            Priority = 0
        };

        return View(model);
    }

    // POST: /Actions/Create
    [HttpPost]
    [Authorize(Policy = "CanEdit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ActionItem model)
    {
        ViewData["Title"] = "New Action";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        model.OwnerId = userId;
        model.CreatedAtUtc = DateTime.UtcNow;
        model.UpdatedAtUtc = DateTime.UtcNow;
        model.Status = string.IsNullOrWhiteSpace(model.Status) ? "Planned" : model.Status;
        model.SourceBook = string.IsNullOrWhiteSpace(model.SourceBook) ? null : model.SourceBook;

        // OwnerId is required on the model but is set server-side (not posted from the form).
        // Remove any model-state error that may have been added during binding.
        ModelState.Remove(nameof(ActionItem.OwnerId));

        if (!ModelState.IsValid) return View(model);

        _db.Actions.Add(model);
        await _db.SaveChangesAsync();
        TempData["ToastSuccess"] = "Action created.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Actions/Edit/5
    [Authorize(Policy = "CanEdit")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        ViewData["Title"] = "Edit Action";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var item = await _db.Actions.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        return item == null ? NotFound() : View(item);
    }

    // POST: /Actions/Edit/5
    [HttpPost]
    [Authorize(Policy = "CanEdit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ActionItem model)
    {
        if (id != model.Id) return NotFound();

        ViewData["Title"] = "Edit Action";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var item = await _db.Actions.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        if (item == null) return NotFound();

        if (!ModelState.IsValid) return View(model);

        var previousStatus = item.Status;

        item.Title = model.Title;
        item.Description = model.Description;
        item.Category = model.Category;
        item.Status = string.IsNullOrWhiteSpace(model.Status) ? item.Status : model.Status;
        item.Priority = model.Priority;
        item.DueAtUtc = model.DueAtUtc;
        item.SourceBook = model.SourceBook;
        item.WorkspaceId = model.WorkspaceId;
        item.StrategyId = model.StrategyId;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Gamification: award XP when an action is marked completed.
        if (!string.Equals(previousStatus, "Completed", StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            // Reuse the user id we already resolved above.
            if (!string.IsNullOrWhiteSpace(userId))
            {
                await _gamification.AwardXpAsync(userId, xp: 25, reason: "Completed an action", sourceType: "Action", sourceId: item.Id, CancellationToken.None);
            }
        }

        TempData["ToastSuccess"] = "Action updated.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Actions/Delete/5
    [Authorize(Policy = "CanEdit")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        ViewData["Title"] = "Delete Action";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var item = await _db.Actions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);

        return item == null ? NotFound() : View(item);
    }

    // POST: /Actions/Delete/5
    [HttpPost, ActionName("Delete")]
    [Authorize(Policy = "CanEdit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var userId = await GetUserIdAsync();
        var item = await _db.Actions.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        if (item == null) return NotFound();

        // Soft delete (DbContext converts Deletes for ISoftDeletable entities).
        _db.Actions.Remove(item);
        await _db.SaveChangesAsync();
        TempData["ToastSuccess"] = "Action deleted.";
        return RedirectToAction(nameof(Index));
    }
}
