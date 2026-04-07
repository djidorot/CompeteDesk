using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Models;
using CompeteDesk.Services;
using CompeteDesk.Services.Billing;
using CompeteDesk.Services.Exports;

namespace CompeteDesk.Controllers;

[Authorize]
public sealed class ExportsController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ExportReportService _exports;
    private readonly ActiveWorkspaceService _activeWorkspace;
    private readonly ApplicationDbContext _db;
    private readonly SubscriptionService _subscriptionService;

    public ExportsController(
        UserManager<IdentityUser> userManager,
        ExportReportService exports,
        ActiveWorkspaceService activeWorkspace,
        ApplicationDbContext db,
        SubscriptionService subscriptionService)
    {
        _userManager = userManager;
        _exports = exports;
        _activeWorkspace = activeWorkspace;
        _db = db;
        _subscriptionService = subscriptionService;
    }

    private async Task<string?> GetUserIdAsync() => (await _userManager.GetUserAsync(User))?.Id;

    private async Task<IActionResult?> EnforceExportQuotaAsync(string userId, CancellationToken ct)
    {
        var quota = await _subscriptionService.CanExportAsync(userId, ct);
        if (quota.Allowed) return null;

        TempData["ExportError"] = quota.Error ?? "You reached your export quota for the current billing period.";
        return RedirectToAction(nameof(Index));
    }

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
                .FirstOrDefaultAsync(x => x.Id == resolvedWorkspaceId.Value && (x.OwnerId == userId || _db.WorkspaceMembers.Any(m => m.WorkspaceId == x.Id && m.UserId == userId)), ct);
        }

        var usage = await _subscriptionService.GetUsageSnapshotAsync(userId, ct);

        return View(new ExportIndexViewModel
        {
            WorkspaceId = workspace?.Id,
            WorkspaceName = workspace?.Name,
            HasWorkspace = workspace is not null,
            Tier = usage.Plan.Tier,
            ExportsUsed = usage.ExportsUsed,
            ExportLimit = usage.Plan.MonthlyExportLimit
        });
    }

    [HttpGet]
    public async Task<IActionResult> CompetencySummaryPdf(int? workspaceId, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var quotaResult = await EnforceExportQuotaAsync(userId, ct);
        if (quotaResult is not null) return quotaResult;

        var resolvedWorkspaceId = await _activeWorkspace.ResolveAsync(HttpContext, userId, workspaceId, ct);
        if (!resolvedWorkspaceId.HasValue)
        {
            TempData["ExportError"] = "Create a workspace first before exporting reports.";
            return RedirectToAction(nameof(Index));
        }

        var bytes = await _exports.ExportCompetencySummaryPdfAsync(userId, resolvedWorkspaceId, ct);
        await _subscriptionService.RecordExportUsageAsync(userId, ct);
        var fileName = $"competency-summary-ws-{resolvedWorkspaceId}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ProgressReportPdf(int? workspaceId, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var quotaResult = await EnforceExportQuotaAsync(userId, ct);
        if (quotaResult is not null) return quotaResult;

        var resolvedWorkspaceId = await _activeWorkspace.ResolveAsync(HttpContext, userId, workspaceId, ct);
        if (!resolvedWorkspaceId.HasValue)
        {
            TempData["ExportError"] = "Create a workspace first before exporting reports.";
            return RedirectToAction(nameof(Index));
        }

        var bytes = await _exports.ExportProgressReportPdfAsync(userId, resolvedWorkspaceId, ct);
        await _subscriptionService.RecordExportUsageAsync(userId, ct);
        var fileName = $"progress-report-ws-{resolvedWorkspaceId}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> StrategiesPdf(int? workspaceId, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        var quotaResult = await EnforceExportQuotaAsync(userId, ct);
        if (quotaResult is not null) return quotaResult;
        var resolvedWorkspaceId = await _activeWorkspace.ResolveAsync(HttpContext, userId, workspaceId, ct);
        var bytes = await _exports.ExportStrategiesPdfAsync(userId, resolvedWorkspaceId, ct);
        await _subscriptionService.RecordExportUsageAsync(userId, ct);
        return File(bytes, "application/pdf", $"strategies-{resolvedWorkspaceId ?? 0}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> StrategiesExcel(int? workspaceId, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        var quotaResult = await EnforceExportQuotaAsync(userId, ct);
        if (quotaResult is not null) return quotaResult;
        var resolvedWorkspaceId = await _activeWorkspace.ResolveAsync(HttpContext, userId, workspaceId, ct);
        var bytes = await _exports.ExportStrategiesCsvAsync(userId, resolvedWorkspaceId, ct);
        await _subscriptionService.RecordExportUsageAsync(userId, ct);
        return File(bytes, "text/csv", $"strategies-{resolvedWorkspaceId ?? 0}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> MonthlySummaryPdf(int? workspaceId, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        var quotaResult = await EnforceExportQuotaAsync(userId, ct);
        if (quotaResult is not null) return quotaResult;
        var resolvedWorkspaceId = await _activeWorkspace.ResolveAsync(HttpContext, userId, workspaceId, ct);
        var bytes = await _exports.ExportMonthlySummaryPdfAsync(userId, resolvedWorkspaceId, ct);
        await _subscriptionService.RecordExportUsageAsync(userId, ct);
        return File(bytes, "application/pdf", $"monthly-summary-{resolvedWorkspaceId ?? 0}.pdf");
    }

    public sealed class ExportIndexViewModel
    {
        public int? WorkspaceId { get; set; }
        public string? WorkspaceName { get; set; }
        public bool HasWorkspace { get; set; }
        public string Tier { get; set; } = "Free";
        public int ExportsUsed { get; set; }
        public int ExportLimit { get; set; }
    }
}
