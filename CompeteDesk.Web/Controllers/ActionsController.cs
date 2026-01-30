using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Models;

namespace CompeteDesk.Controllers;

[Authorize]
public class ActionsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public ActionsController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private async Task<string> GetUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id ?? string.Empty;
    }

    // GET: /Actions
    public async Task<IActionResult> Index(string? q, string status = "Planned")
    {
        ViewData["Title"] = "Actions";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var query = _db.Actions
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
                x.Title.Contains(term) ||
                (x.Description != null && x.Description.Contains(term)) ||
                (x.Category != null && x.Category.Contains(term)));
        }

        var items = await query
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.DueAtUtc ?? DateTime.MaxValue)
            .ThenByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
            .ThenBy(x => x.Title)
            .ToListAsync();

        ViewBag.Query = q ?? string.Empty;
        ViewBag.Status = status;

        return View(items);
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

        if (!ModelState.IsValid) return View(model);

        _db.Actions.Add(model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // GET: /Actions/Edit/5
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
        return RedirectToAction(nameof(Index));
    }

    // GET: /Actions/Delete/5
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var userId = await GetUserIdAsync();
        var item = await _db.Actions.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        if (item == null) return NotFound();

        _db.Actions.Remove(item);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
