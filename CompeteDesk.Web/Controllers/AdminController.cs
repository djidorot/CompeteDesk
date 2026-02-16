using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.ViewModels.Admin;

namespace CompeteDesk.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public AdminController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // GET: /Admin
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var vm = new AdminDashboardViewModel
        {
            Users = await _db.Users.CountAsync(ct),
            Workspaces = await _db.Workspaces.CountAsync(ct),
            Strategies = await _db.Strategies.CountAsync(ct),
            Actions = await _db.Actions.CountAsync(ct),
            WarIntel = await _db.WarIntel.CountAsync(ct),
            WarPlans = await _db.WarPlans.CountAsync(ct),
            WebsiteReports = await _db.WebsiteAnalysisReports.CountAsync(ct),
            BusinessReports = await _db.BusinessAnalysisReports.CountAsync(ct),
            DecisionTraces = await _db.DecisionTraces.CountAsync(ct)
        };

        vm.RecentUsers = await _db.Users
            .AsNoTracking()
            .OrderByDescending(u => u.Id)
            .Take(10)
            .Select(u => new RecentUserItem
            {
                Id = u.Id,
                Email = u.Email,
                UserName = u.UserName
            })
            .ToListAsync(ct);

        vm.RecentDecisionTraces = await _db.DecisionTraces
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(10)
            .ToListAsync(ct);

        ViewData["Title"] = "Admin";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        return View(vm);
    }

    // GET: /Admin/Users
    public async Task<IActionResult> Users(CancellationToken ct)
    {
        ViewData["Title"] = "User Roles";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var users = await _db.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync(ct);
        var vm = new AdminUsersViewModel();

        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var role = roles.Contains(IdentitySeeder.AdminRoleName) ? IdentitySeeder.AdminRoleName
                : roles.Contains(IdentitySeeder.ReadOnlyRoleName) ? IdentitySeeder.ReadOnlyRoleName
                : IdentitySeeder.EditorRoleName;

            vm.Users.Add(new AdminUserItem
            {
                Id = u.Id,
                Email = u.Email,
                UserName = u.UserName,
                Role = role
            });
        }

        return View(vm);
    }

    // POST: /Admin/SetRole
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(string id, string role, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id)) return RedirectToAction(nameof(Users));

        role = (role ?? "").Trim();
        var allowed = new[] { IdentitySeeder.AdminRoleName, IdentitySeeder.EditorRoleName, IdentitySeeder.ReadOnlyRoleName };
        if (!allowed.Contains(role))
        {
            TempData["ToastError"] = "Invalid role.";
            return RedirectToAction(nameof(Users));
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return RedirectToAction(nameof(Users));

        var currentRoles = await _userManager.GetRolesAsync(user);
        // Enforce single app role (Admin/Editor/ReadOnly)
        var toRemove = currentRoles.Where(r => allowed.Contains(r)).ToArray();
        if (toRemove.Length > 0)
            await _userManager.RemoveFromRolesAsync(user, toRemove);

        await _userManager.AddToRoleAsync(user, role);

        TempData["ToastSuccess"] = $"Updated role for {user.Email} to {role}.";
        return RedirectToAction(nameof(Users));
    }
}
