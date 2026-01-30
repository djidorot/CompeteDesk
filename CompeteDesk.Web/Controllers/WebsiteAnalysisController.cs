using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Models;
using CompeteDesk.Services.WebsiteAnalysis;
using CompeteDesk.ViewModels.WebsiteAnalysis;

namespace CompeteDesk.Controllers;

[Authorize]
public sealed class WebsiteAnalysisController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly WebsiteAnalysisService _svc;

    public WebsiteAnalysisController(ApplicationDbContext db, UserManager<IdentityUser> userManager, WebsiteAnalysisService svc)
    {
        _db = db;
        _userManager = userManager;
        _svc = svc;
    }

    private async Task<string> GetUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id ?? string.Empty;
    }

    // GET: /WebsiteAnalysis
    public async Task<IActionResult> Index(int? workspaceId)
    {
        ViewData["Title"] = "Website Analysis";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var query = _db.WebsiteAnalysisReports.AsNoTracking()
            .Where(x => x.OwnerId == userId);

        if (workspaceId.HasValue)
            query = query.Where(x => x.WorkspaceId == workspaceId.Value);

        var recent = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(12)
            .ToListAsync();

        var vm = new WebsiteAnalysisIndexViewModel
        {
            WorkspaceId = workspaceId,
            Latest = recent.FirstOrDefault(),
            Recent = recent
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Analyze(WebsiteAnalysisIndexViewModel input, CancellationToken ct)
    {
        ViewData["Title"] = "Website Analysis";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        try
        {
            var report = await _svc.AnalyzeAndSaveAsync(input.Url, userId, input.WorkspaceId, ct);
            return RedirectToAction(nameof(Details), new { id = report.Id });
        }
        catch (Exception ex)
        {
            // re-render Index with error + recent history
            var recent = await _db.WebsiteAnalysisReports.AsNoTracking()
                .Where(x => x.OwnerId == userId)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(12)
                .ToListAsync();

            var vm = new WebsiteAnalysisIndexViewModel
            {
                Url = input.Url,
                WorkspaceId = input.WorkspaceId,
                Latest = recent.FirstOrDefault(),
                Recent = recent,
                Error = ex.Message
            };

            return View(nameof(Index), vm);
        }
    }

    // GET: /WebsiteAnalysis/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        ViewData["Title"] = "Website Report";
        ViewData["LayoutFluid"] = true;
        ViewData["UseSidebar"] = true;

        var userId = await GetUserIdAsync();
        var item = await _db.WebsiteAnalysisReports.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);

        if (item == null) return NotFound();

        // Try parse AI JSON (optional) for easy rendering
        object? ai = null;
        if (!string.IsNullOrWhiteSpace(item.AiInsightsJson))
        {
            try
            {
                ai = JsonSerializer.Deserialize<JsonElement>(item.AiInsightsJson);
            }
            catch
            {
                ai = null;
            }
        }

        ViewData["AiJson"] = ai;
        return View(item);
    }
}
