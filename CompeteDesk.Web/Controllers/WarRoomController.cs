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
public class WarRoomController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public WarRoomController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private async Task<string> GetUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id ?? string.Empty;
    }

    // GET: /WarRoom?tab=intel|plans
    public async Task<IActionResult> Index(string tab = "intel", string? q = null)
    {
        ViewData["Title"] = "War Room";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        tab = (tab ?? "intel").Trim().ToLowerInvariant();
        if (tab != "intel" && tab != "plans") tab = "intel";

        ViewBag.Tab = tab;
        ViewBag.Query = q ?? string.Empty;

        // Keep payload small: only one list at a time.
        if (tab == "plans")
        {
            var query = _db.WarPlans.AsNoTracking().Where(x => x.OwnerId == userId);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(x => x.Name.Contains(term)
                    || (x.Objective != null && x.Objective.Contains(term))
                    || (x.Approach != null && x.Approach.Contains(term))
                    || (x.Status != null && x.Status.Contains(term)));
            }

            var plans = await query
                .OrderBy(x => x.Status == "Active" ? 0 : x.Status == "Draft" ? 1 : 2)
                .ThenByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
                .ThenBy(x => x.Name)
                .ToListAsync();

            return View("IndexPlans", plans);
        }
        else
        {
            var query = _db.WarIntel.AsNoTracking().Where(x => x.OwnerId == userId);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(x => x.Title.Contains(term)
                    || (x.Subject != null && x.Subject.Contains(term))
                    || (x.Signal != null && x.Signal.Contains(term))
                    || (x.Tags != null && x.Tags.Contains(term)));
            }

            var intel = await query
                .OrderByDescending(x => x.ObservedAtUtc ?? x.CreatedAtUtc)
                .ThenByDescending(x => x.Confidence)
                .ThenByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
                .ThenBy(x => x.Title)
                .ToListAsync();

            return View("IndexIntel", intel);
        }
    }

    // --------------------------
    // Intel CRUD
    // --------------------------

    // GET: /WarRoom/IntelCreate
    public IActionResult IntelCreate()
    {
        ViewData["Title"] = "New Intel";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        return View(new WarIntel
        {
            Confidence = 3,
            ObservedAtUtc = DateTime.UtcNow
        });
    }

    // POST: /WarRoom/IntelCreate
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IntelCreate(WarIntel model)
    {
        ViewData["Title"] = "New Intel";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        model.OwnerId = userId;
        model.CreatedAtUtc = DateTime.UtcNow;
        model.UpdatedAtUtc = DateTime.UtcNow;

        if (!ModelState.IsValid) return View(model);

        _db.WarIntel.Add(model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { tab = "intel" });
    }

    // GET: /WarRoom/IntelDetails/5
    public async Task<IActionResult> IntelDetails(int? id)
    {
        if (id == null) return NotFound();

        ViewData["Title"] = "Intel Details";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var item = await _db.WarIntel.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        return item == null ? NotFound() : View(item);
    }

    // GET: /WarRoom/IntelEdit/5
    public async Task<IActionResult> IntelEdit(int? id)
    {
        if (id == null) return NotFound();

        ViewData["Title"] = "Edit Intel";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var item = await _db.WarIntel.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        return item == null ? NotFound() : View(item);
    }

    // POST: /WarRoom/IntelEdit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IntelEdit(int id, WarIntel model)
    {
        if (id != model.Id) return NotFound();

        ViewData["Title"] = "Edit Intel";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var item = await _db.WarIntel.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        if (item == null) return NotFound();

        if (!ModelState.IsValid) return View(model);

        item.Title = model.Title;
        item.Subject = model.Subject;
        item.Signal = model.Signal;
        item.Source = model.Source;
        item.Confidence = model.Confidence;
        item.Tags = model.Tags;
        item.Notes = model.Notes;
        item.ObservedAtUtc = model.ObservedAtUtc;
        item.WorkspaceId = model.WorkspaceId;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { tab = "intel" });
    }

    // GET: /WarRoom/IntelDelete/5
    public async Task<IActionResult> IntelDelete(int? id)
    {
        if (id == null) return NotFound();

        ViewData["Title"] = "Delete Intel";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var item = await _db.WarIntel.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        return item == null ? NotFound() : View(item);
    }

    // POST: /WarRoom/IntelDelete/5
    [HttpPost, ActionName("IntelDelete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IntelDeleteConfirmed(int id)
    {
        var userId = await GetUserIdAsync();
        var item = await _db.WarIntel.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        if (item == null) return NotFound();

        _db.WarIntel.Remove(item);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { tab = "intel" });
    }

    // --------------------------
    // Plans CRUD
    // --------------------------

    // GET: /WarRoom/PlanCreate
    public IActionResult PlanCreate()
    {
        ViewData["Title"] = "New Plan";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        return View(new WarPlan
        {
            Status = "Draft",
            SourceBook = null
        });
    }

    // POST: /WarRoom/PlanCreate
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlanCreate(WarPlan model)
    {
        ViewData["Title"] = "New Plan";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        model.OwnerId = userId;
        model.CreatedAtUtc = DateTime.UtcNow;
        model.UpdatedAtUtc = DateTime.UtcNow;
        model.Status = string.IsNullOrWhiteSpace(model.Status) ? "Draft" : model.Status;
        model.SourceBook = string.IsNullOrWhiteSpace(model.SourceBook) ? null : model.SourceBook;

        if (!ModelState.IsValid) return View(model);

        _db.WarPlans.Add(model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { tab = "plans" });
    }

    // GET: /WarRoom/PlanDetails/5
    public async Task<IActionResult> PlanDetails(int? id)
    {
        if (id == null) return NotFound();

        ViewData["Title"] = "Plan Details";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var item = await _db.WarPlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        return item == null ? NotFound() : View(item);
    }

    // GET: /WarRoom/PlanEdit/5
    public async Task<IActionResult> PlanEdit(int? id)
    {
        if (id == null) return NotFound();

        ViewData["Title"] = "Edit Plan";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var item = await _db.WarPlans.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        return item == null ? NotFound() : View(item);
    }

    // POST: /WarRoom/PlanEdit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlanEdit(int id, WarPlan model)
    {
        if (id != model.Id) return NotFound();

        ViewData["Title"] = "Edit Plan";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var item = await _db.WarPlans.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        if (item == null) return NotFound();

        if (!ModelState.IsValid) return View(model);

        item.Name = model.Name;
        item.Objective = model.Objective;
        item.Approach = model.Approach;
        item.Assumptions = model.Assumptions;
        item.Risks = model.Risks;
        item.Contingencies = model.Contingencies;
        item.Status = string.IsNullOrWhiteSpace(model.Status) ? item.Status : model.Status;
        item.StartAtUtc = model.StartAtUtc;
        item.EndAtUtc = model.EndAtUtc;
        item.SourceBook = model.SourceBook;
        item.WorkspaceId = model.WorkspaceId;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { tab = "plans" });
    }

    // GET: /WarRoom/PlanDelete/5
    public async Task<IActionResult> PlanDelete(int? id)
    {
        if (id == null) return NotFound();

        ViewData["Title"] = "Delete Plan";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var item = await _db.WarPlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        return item == null ? NotFound() : View(item);
    }

    // POST: /WarRoom/PlanDelete/5
    [HttpPost, ActionName("PlanDelete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlanDeleteConfirmed(int id)
    {
        var userId = await GetUserIdAsync();
        var item = await _db.WarPlans.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        if (item == null) return NotFound();

        _db.WarPlans.Remove(item);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { tab = "plans" });
    }
}
