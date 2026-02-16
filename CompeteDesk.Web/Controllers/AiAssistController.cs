using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CompeteDesk.Services.Ai;

namespace CompeteDesk.Controllers;

[Authorize]
public sealed class AiAssistController : Controller
{
    private readonly StrategyAiAssistService _strategyAssist;
    private readonly UserManager<IdentityUser> _userManager;

    public AiAssistController(StrategyAiAssistService strategyAssist, UserManager<IdentityUser> userManager)
    {
        _strategyAssist = strategyAssist;
        _userManager = userManager;
    }

    public sealed class AssistRequest
    {
        public string? Goal { get; set; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StrategySwot(int id, [FromBody] AssistRequest? req, CancellationToken ct)
        => await RunStrategyAssist(id, StrategyAiAssistService.AssistKind.Swot, req?.Goal, ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StrategyStudySummary(int id, [FromBody] AssistRequest? req, CancellationToken ct)
        => await RunStrategyAssist(id, StrategyAiAssistService.AssistKind.StudySummary, req?.Goal, ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StrategyQuiz(int id, [FromBody] AssistRequest? req, CancellationToken ct)
        => await RunStrategyAssist(id, StrategyAiAssistService.AssistKind.Quiz, req?.Goal, ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StrategyImprovements(int id, [FromBody] AssistRequest? req, CancellationToken ct)
        => await RunStrategyAssist(id, StrategyAiAssistService.AssistKind.Improvements, req?.Goal, ct);

    private async Task<IActionResult> RunStrategyAssist(int id, StrategyAiAssistService.AssistKind kind, string? goal, CancellationToken ct)
    {
        if (!_strategyAssist.IsConfigured)
            return BadRequest(new { error = "OpenAI is not configured. Set OpenAI:ApiKey." });

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        try
        {
            var result = await _strategyAssist.GenerateAsync(userId, id, kind, goal, ct);
            return Ok(new
            {
                ok = true,
                kind = result.Kind.ToString(),
                traceId = result.TraceId,
                outputJson = result.OutputJson
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
