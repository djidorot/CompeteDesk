using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Models;
using CompeteDesk.Services;
using CompeteDesk.Services.Exports;

namespace CompeteDesk.Controllers;

[Authorize]
public sealed class ExportsController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ExportReportService _exports;
    private readonly ActiveWorkspaceService _activeWorkspace;
    private readonly ApplicationDbContext _db;

    public ExportsController(
        UserManager<IdentityUser> userManager,
        ExportReportService exports,
        ActiveWorkspaceService activeWorkspace,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _exports = exports;
        _activeWorkspace = activeWorkspace;
        _db = db;
    }

    private async Task<string?> GetUserIdAsync() => (await _userManager.GetUserAsync(User))?.Id;

    [HttpGet]
    public async Task<IActionResult> Index(int? workspaceId, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var resolvedWorkspaceId = await _activeWorkspace.ResolveAsync(HttpContext, userId, workspaceId, ct);
        Workspace? workspace = null;

        if (resolvedWorkspaceId.HasValue)
        {
            workspace = await _db.Workspaces
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == resolvedWorkspaceId.Value && x.OwnerId == userId, ct);
        }

        return View(new ExportIndexViewModel
        {
            WorkspaceId = workspace?.Id,
            WorkspaceName = workspace?.Name,
            HasWorkspace = workspace is not null
        });
    }

    [HttpGet]
    public async Task<IActionResult> CompetencySummaryPdf(int? workspaceId, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var resolvedWorkspaceId = await _activeWorkspace.ResolveAsync(HttpContext, userId, workspaceId, ct);
        if (!resolvedWorkspaceId.HasValue)
        {
            TempData["ExportError"] = "Create a workspace first before exporting reports.";
            return RedirectToAction(nameof(Index));
        }

        var bytes = await _exports.ExportCompetencySummaryPdfAsync(userId, resolvedWorkspaceId, ct);
        var fileName = $"competency-summary-ws-{resolvedWorkspaceId}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ProgressReportPdf(int? workspaceId, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var resolvedWorkspaceId = await _activeWorkspace.ResolveAsync(HttpContext, userId, workspaceId, ct);
        if (!resolvedWorkspaceId.HasValue)
        {
            TempData["ExportError"] = "Create a workspace first before exporting reports.";
            return RedirectToAction(nameof(Index));
        }

        var bytes = await _exports.ExportProgressReportPdfAsync(userId, resolvedWorkspaceId, ct);
        var fileName = $"progress-report-ws-{resolvedWorkspaceId}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    public sealed class ExportIndexViewModel
    {
        public int? WorkspaceId { get; set; }
        public string? WorkspaceName { get; set; }
        public bool HasWorkspace { get; set; }
    }
}
