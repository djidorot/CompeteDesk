using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CompeteDesk.Services.WarRoom;

namespace CompeteDesk.Controllers;

/// <summary>
/// Thin JSON endpoints for War Room AI actions.
/// These are called by wwwroot/js/warroom.js.
/// </summary>
[Authorize]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class WarRoomAiController : Controller
{
    private readonly WarRoomAiService _ai;
    private readonly UserManager<IdentityUser> _userManager;

    public WarRoomAiController(WarRoomAiService ai, UserManager<IdentityUser> userManager)
    {
        _ai = ai;
        _userManager = userManager;
    }

    private async Task<string> GetUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id ?? string.Empty;
    }

    public sealed class IntelBriefRequest
    {
        public int[] IntelIds { get; set; } = Array.Empty<int>();
    }

    public sealed class RedTeamPlanRequest
    {
        public int PlanId { get; set; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IntelBrief([FromBody] IntelBriefRequest req, CancellationToken ct)
    {
        if (!_ai.IsConfigured)
            return BadRequest(new { error = "OpenAI is not configured. Set OpenAI:ApiKey." });

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var intelIds = (req?.IntelIds ?? Array.Empty<int>()).Distinct().Take(20).ToArray();
        var doc = await _ai.CreateIntelBriefAsync(userId, intelIds, ct);
        return Content(doc.RootElement.GetRawText(), "application/json");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RedTeamPlan([FromBody] RedTeamPlanRequest req, CancellationToken ct)
    {
        if (!_ai.IsConfigured)
            return BadRequest(new { error = "OpenAI is not configured. Set OpenAI:ApiKey." });

        var userId = await GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        if (req == null || req.PlanId <= 0)
            return BadRequest(new { error = "Invalid plan id." });

        var doc = await _ai.RedTeamPlanAsync(userId, req.PlanId, ct);
        return Content(doc.RootElement.GetRawText(), "application/json");
    }
}
