using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Models;
using CompeteDesk.ViewModels.Workspaces;

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
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Workspaces";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var items = await _db.Workspaces
            .AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .OrderByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return View(items);
    }

    // GET: /Workspaces/Create
    public IActionResult Create()
    {
        ViewData["Title"] = "New Workspace";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        return View(new CreateWorkspaceViewModel());
    }

    // POST: /Workspaces/Create
    [HttpPost]
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
