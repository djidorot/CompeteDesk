using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CompeteDesk.Services.Exports;

namespace CompeteDesk.Controllers;

[Authorize]
public sealed class ExportsController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ExportReportService _exports;

    public ExportsController(UserManager<IdentityUser> userManager, ExportReportService exports)
    {
        _userManager = userManager;
        _exports = exports;
    }

    private async Task<string?> GetUserIdAsync() => (await _userManager.GetUserAsync(User))?.Id;

    [HttpGet]
    public async Task<IActionResult> CompetencySummaryPdf(int? workspaceId, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var bytes = await _exports.ExportCompetencySummaryPdfAsync(userId, workspaceId, ct);
        var fileName = workspaceId.HasValue ? $"competency-summary-ws-{workspaceId}.pdf" : "competency-summary.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ProgressReportPdf(int? workspaceId, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var bytes = await _exports.ExportProgressReportPdfAsync(userId, workspaceId, ct);
        var fileName = workspaceId.HasValue ? $"progress-report-ws-{workspaceId}.pdf" : "progress-report.pdf";
        return File(bytes, "application/pdf", fileName);
    }
}
