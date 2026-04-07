using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.ViewModels.Admin;
using CompeteDesk.Services.Security;

namespace CompeteDesk.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly FeaturePermissionService _permissionService;

    public AdminController(ApplicationDbContext db, UserManager<IdentityUser> userManager, FeaturePermissionService permissionService)
    {
        _db = db;
        _userManager = userManager;
        _permissionService = permissionService;
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
            DecisionTraces = await _db.DecisionTraces.CountAsync(ct),
            PermissionOverrides = await _db.UserFeaturePermissions.CountAsync(x => x.IsGranted, ct)
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
        var explicitPermissionCounts = await _db.UserFeaturePermissions
            .AsNoTracking()
            .Where(x => x.IsGranted)
            .GroupBy(x => x.UserId)
            .Select(x => new { UserId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var vm = new AdminUsersViewModel();

        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var role = roles.Contains(IdentitySeeder.AdminRoleName) ? IdentitySeeder.AdminRoleName
                : roles.Contains(IdentitySeeder.EditorRoleName) ? IdentitySeeder.EditorRoleName
                : roles.Contains(IdentitySeeder.ReadOnlyRoleName) ? IdentitySeeder.ReadOnlyRoleName
                : IdentitySeeder.UserRoleName;

            vm.Users.Add(new AdminUserItem
            {
                Id = u.Id,
                Email = u.Email,
                UserName = u.UserName,
                Role = role,
                GrantedPermissions = explicitPermissionCounts.TryGetValue(u.Id, out var count) ? count : 0
            });
        }

        return View(vm);
    }

    public async Task<IActionResult> Permissions(string id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id)) return RedirectToAction(nameof(Users));

        ViewData["Title"] = "Feature Permissions";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return RedirectToAction(nameof(Users));

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.Contains(IdentitySeeder.AdminRoleName) ? IdentitySeeder.AdminRoleName
            : roles.Contains(IdentitySeeder.EditorRoleName) ? IdentitySeeder.EditorRoleName
            : roles.Contains(IdentitySeeder.ReadOnlyRoleName) ? IdentitySeeder.ReadOnlyRoleName
            : IdentitySeeder.UserRoleName;

        var effective = await _permissionService.GetEffectivePermissionsAsync(user, ct);
        var vm = new AdminPermissionsViewModel
        {
            UserId = user.Id,
            Email = user.Email,
            UserName = user.UserName,
            Role = role,
            Groups = FeaturePermissionService.Catalog
                .GroupBy(x => x.Group)
                .OrderBy(x => x.Key)
                .Select(g => new AdminPermissionGroup
                {
                    Name = g.Key,
                    Permissions = g
                        .OrderBy(x => x.FeatureName)
                        .Select(x => new AdminPermissionItem
                        {
                            Key = x.Key,
                            FeatureName = x.FeatureName,
                            Description = x.Description,
                            IsGranted = effective.TryGetValue(x.Key, out var isGranted) && isGranted
                        })
                        .ToList()
                })
                .ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePermissions(string userId, List<string> grantedPermissions, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) return RedirectToAction(nameof(Users));

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return RedirectToAction(nameof(Users));

        await _permissionService.SavePermissionsAsync(userId, grantedPermissions, ct);
        TempData["ToastSuccess"] = $"Updated feature permissions for {user.Email}.";
        return RedirectToAction(nameof(Permissions), new { id = userId });
    }

    // POST: /Admin/SetRole
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(string id, string role, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id)) return RedirectToAction(nameof(Users));

        role = (role ?? "").Trim();
        var allowed = new[] { IdentitySeeder.UserRoleName, IdentitySeeder.AdminRoleName, IdentitySeeder.EditorRoleName, IdentitySeeder.ReadOnlyRoleName };
        if (!allowed.Contains(role))
        {
            TempData["ToastError"] = "Invalid role.";
            return RedirectToAction(nameof(Users));
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return RedirectToAction(nameof(Users));

        var currentRoles = await _userManager.GetRolesAsync(user);
        var toRemove = currentRoles.Where(r => allowed.Contains(r)).ToArray();
        if (toRemove.Length > 0)
            await _userManager.RemoveFromRolesAsync(user, toRemove);

        await _userManager.AddToRoleAsync(user, role);

        TempData["ToastSuccess"] = $"Updated role for {user.Email} to {role}.";
        return RedirectToAction(nameof(Users));
    }
}
