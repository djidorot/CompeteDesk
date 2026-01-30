// FILE: CompeteDesk/Services/StrategyCopilot/StrategyCopilotAiService.cs
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Services.Ai;
using CompeteDesk.Services.OpenAI;

namespace CompeteDesk.Services.StrategyCopilot;

/// <summary>
/// Cross-feature AI "Strategy Co-Pilot" that synthesizes Strategy + Intel + (optional) Strategy Canvas + ERRC.
/// Output is strict JSON and is always logged via DecisionTraceService.
/// </summary>
public sealed class StrategyCopilotAiService
{
    private readonly ApplicationDbContext _db;
    private readonly OpenAiChatClient _ai;
    private readonly DecisionTraceService _trace;
    private readonly AiContextPackBuilder _contextPack;

    public StrategyCopilotAiService(
        ApplicationDbContext db,
        OpenAiChatClient ai,
        DecisionTraceService trace,
        AiContextPackBuilder contextPack)
    {
        _db = db;
        _ai = ai;
        _trace = trace;
        _contextPack = contextPack;
    }

    public bool IsConfigured => _ai.IsConfigured;

    public sealed class CopilotRequest
    {
        public int? WorkspaceId { get; set; }
        public int[] IntelIds { get; set; } = Array.Empty<int>();
        public int[] StrategyIds { get; set; } = Array.Empty<int>();

        // Optional user-provided artifacts.
        public string? StrategyCanvasText { get; set; }
        public string? ErrcEliminate { get; set; }
        public string? ErrcReduce { get; set; }
        public string? ErrcRaise { get; set; }
        public string? ErrcCreate { get; set; }

        public string? Goal { get; set; }
        public string? MarketScope { get; set; }
        public string? Constraints { get; set; }
    }

    public async Task<JsonDocument> GenerateAsync(string ownerId, CopilotRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return JsonDocument.Parse("{\"error\":\"Unauthorized.\"}");

        req ??= new CopilotRequest();
        var workspaceId = req.WorkspaceId;

        var intelIds = (req.IntelIds ?? Array.Empty<int>()).Distinct().Take(30).ToArray();
        var strategyIds = (req.StrategyIds ?? Array.Empty<int>()).Distinct().Take(30).ToArray();

        var intel = intelIds.Length == 0
            ? Array.Empty<object>()
            : await _db.WarIntel
                .AsNoTracking()
                .Where(x => x.OwnerId == ownerId && intelIds.Contains(x.Id))
                .OrderByDescending(x => x.ObservedAtUtc ?? x.CreatedAtUtc)
                .Take(30)
                .Select(i => new
                {
                    i.Id,
                    i.WorkspaceId,
                    i.Title,
                    i.Subject,
                    i.Signal,
                    i.Source,
                    i.Tags,
                    i.Confidence,
                    i.ObservedAtUtc,
                    notes = string.IsNullOrWhiteSpace(i.Notes)
                        ? null
                        : (i.Notes!.Length > 900 ? i.Notes.Substring(0, 900) : i.Notes)
                })
                .Cast<object>()
                .ToArrayAsync(ct);

        // If workspaceId isn't provided, infer from selected intel/strategies.
        if (!workspaceId.HasValue)
        {
            var inferred = intel.OfType<dynamic>().FirstOrDefault()?.WorkspaceId;
            if (inferred != null)
            {
                try { workspaceId = (int?)inferred; } catch { /* ignore */ }
            }
        }

        var strategies = strategyIds.Length == 0
            ? Array.Empty<object>()
            : await _db.Strategies
                .AsNoTracking()
                .Where(s => s.OwnerId == ownerId && strategyIds.Contains(s.Id))
                .OrderByDescending(s => s.UpdatedAtUtc ?? s.CreatedAtUtc)
                .Take(30)
                .Select(s => new
                {
                    s.Id,
                    s.WorkspaceId,
                    s.Name,
                    s.Category,
                    s.CorePrinciple,
                    s.Summary,
                    s.Priority,
                    aiSummary = s.AiSummary
                })
                .Cast<object>()
                .ToArrayAsync(ct);

        if (!workspaceId.HasValue)
        {
            var inferred = strategies.OfType<dynamic>().FirstOrDefault()?.WorkspaceId;
            if (inferred != null)
            {
                try { workspaceId = (int?)inferred; } catch { /* ignore */ }
            }
        }

        var ctx = await _contextPack.BuildAsync(ownerId, workspaceId, ct);

        var payload = new
        {
            intent = "blue_ocean_strategy_copilot",
            nowUtc = DateTime.UtcNow,
            workspaceId,
            goal = string.IsNullOrWhiteSpace(req.Goal) ? null : req.Goal,
            marketScope = string.IsNullOrWhiteSpace(req.MarketScope) ? null : req.MarketScope,
            constraints = string.IsNullOrWhiteSpace(req.Constraints) ? null : req.Constraints,

            contextPack = JsonDocument.Parse(ctx).RootElement,

            selectedStrategies = strategies,
            selectedIntel = intel,

            userArtifacts = new
            {
                strategyCanvas = string.IsNullOrWhiteSpace(req.StrategyCanvasText) ? null : req.StrategyCanvasText,
                errc = new
                {
                    eliminate = string.IsNullOrWhiteSpace(req.ErrcEliminate) ? null : req.ErrcEliminate,
                    reduce = string.IsNullOrWhiteSpace(req.ErrcReduce) ? null : req.ErrcReduce,
                    raise = string.IsNullOrWhiteSpace(req.ErrcRaise) ? null : req.ErrcRaise,
                    create = string.IsNullOrWhiteSpace(req.ErrcCreate) ? null : req.ErrcCreate
                }
            }
        };

        var systemPrompt =
            "You are an expert Blue Ocean Strategy co-pilot. Output STRICT JSON only. " +
            "Synthesize the provided strategy canvas text, ERRC grid, selected strategies, and war intel to generate " +
            "high-quality Blue Ocean hypotheses and a strategic narrative. " +
            "Be practical, specific, and avoid generic advice. " +
            "When information is missing, propose the smallest experiments to learn fast. " +
            "Return JSON matching this schema:\n" +
            "{\n" +
            "  \"headline\": string,\n" +
            "  \"blueOceanHypotheses\": [\n" +
            "    {\"title\": string, \"whoItUnlocks\": string, \"valueLeap\": string, \"howToWin\": string, \"keyRisks\": [string], \"quickExperiments\": [string]}\n" +
            "  ],\n" +
            "  \"strategicNarrative\": {\"situation\": string, \"insight\": string, \"choice\": string, \"moves\": [string], \"successSignals\": [string]},\n" +
            "  \"errcRecommendations\": {\"eliminate\": [string], \"reduce\": [string], \"raise\": [string], \"create\": [string]},\n" +
            "  \"strategyCanvasDeltas\": [string],\n" +
            "  \"noncustomerTargets\": [string],\n" +
            "  \"next90Days\": [string],\n" +
            "  \"overallConfidence1to5\": number\n" +
            "}";

        var inputJson = JsonSerializer.Serialize(payload);

        var startedUtc = DateTime.UtcNow;
        var result = await _ai.CreateJsonInsightsAsync(systemPrompt, inputJson, ct);
        var durationMs = (int)(DateTime.UtcNow - startedUtc).TotalMilliseconds;

        try
        {
            await _trace.LogAsync(
                ownerId: ownerId,
                workspaceId: workspaceId,
                feature: "StrategyCopilot.Generate",
                input: new
                {
                    systemPrompt,
                    inputJson,
                    durationMs,
                    intelCount = intel.Length,
                    strategyCount = strategies.Length,
                    hasCanvas = !string.IsNullOrWhiteSpace(req.StrategyCanvasText),
                    hasErrc = !(string.IsNullOrWhiteSpace(req.ErrcEliminate)
                               && string.IsNullOrWhiteSpace(req.ErrcReduce)
                               && string.IsNullOrWhiteSpace(req.ErrcRaise)
                               && string.IsNullOrWhiteSpace(req.ErrcCreate))
                },
                outputJson: result,
                entityType: "Workspace",
                entityId: workspaceId,
                entityTitle: "AI Strategy Co-Pilot",
                ct: ct
            );
        }
        catch
        {
            // Never block user flows.
        }

        return JsonDocument.Parse(result);
    }
}
