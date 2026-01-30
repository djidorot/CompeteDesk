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

    public AdminController(ApplicationDbContext db)
    {
        _db = db;
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
}
