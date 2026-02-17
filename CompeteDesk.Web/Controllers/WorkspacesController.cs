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
using CompeteDesk.ViewModels.Workspaces;
using CompeteDesk.Models.Common;

namespace CompeteDesk.Controllers;

[Authorize]
public class WorkspacesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public WorkspacesController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private async Task<string> GetUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id ?? string.Empty;
    }

    // GET: /Workspaces
    public async Task<IActionResult> Index(int page = 1, int pageSize = 25, bool partial = false, CancellationToken ct = default)
    {
        ViewData["Title"] = "Workspaces";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var query = _db.Workspaces
            .AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .OrderByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
            .ThenBy(x => x.Name)
            ;

        var paged = await PagedResult<Workspace>.CreateAsync(query, page, pageSize, ct);

        ViewBag.Page = paged.Page;
        ViewBag.PageSize = paged.PageSize;
        ViewBag.TotalCount = paged.TotalCount;
        ViewBag.TotalPages = paged.TotalPages;

        if (partial || Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_WorkspacesList", paged.Items.ToList());
        return View(paged.Items.ToList());
    }

    // GET: /Workspaces/Create
    [Authorize(Policy = "CanEdit")]
    public IActionResult Create()
    {
        ViewData["Title"] = "New Workspace";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        return View(new CreateWorkspaceViewModel());
    }

    // POST: /Workspaces/Create
    [HttpPost]
    [Authorize(Policy = "CanEdit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateWorkspaceViewModel vm)
    {
        ViewData["Title"] = "New Workspace";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var name = (vm.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(vm.Name), "Workspace name is required.");
            return View(vm);
        }

        // Prevent duplicates for this user.
        var exists = await _db.Workspaces.AnyAsync(x => x.OwnerId == userId && x.Name == name);
        if (exists)
        {
            ModelState.AddModelError(nameof(vm.Name), "You already have a workspace with that name.");
            return View(vm);
        }

        var workspace = new Workspace
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim(),
            OwnerId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Workspaces.Add(workspace);
        await _db.SaveChangesAsync();

        TempData["ToastSuccess"] = "Workspace created.";
        return RedirectToAction("Index", "Dashboard");
    }
}
