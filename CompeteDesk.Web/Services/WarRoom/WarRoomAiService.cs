// FILE: CompeteDesk/Services/WarRoom/WarRoomAiService.cs
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Services.OpenAI;
using CompeteDesk.Services.Ai;

namespace CompeteDesk.Services.WarRoom;

public sealed class WarRoomAiService
{
    private readonly ApplicationDbContext _db;
    private readonly OpenAiChatClient _ai;
    private readonly DecisionTraceService _trace;
    private readonly AiContextPackBuilder _contextPack;

    public WarRoomAiService(
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

    public async Task<JsonDocument> CreateIntelBriefAsync(string ownerId, int[] intelIds, CancellationToken ct)
    {
        intelIds ??= Array.Empty<int>();

        var items = await _db.WarIntel
            .AsNoTracking()
            .Where(x => x.OwnerId == ownerId && intelIds.Contains(x.Id))
            .OrderByDescending(x => x.ObservedAtUtc ?? x.CreatedAtUtc)
            .ToListAsync(ct);

        if (items.Count == 0)
            return JsonDocument.Parse("{\"error\":\"No intel selected.\"}");

        var workspaceId = items.FirstOrDefault()?.WorkspaceId;

        var ctx = await _contextPack.BuildAsync(ownerId, workspaceId, ct);

        var payload = new
        {
            intent = "intel_brief",
            nowUtc = DateTime.UtcNow,
            contextPack = JsonDocument.Parse(ctx).RootElement,
            intel = items.Select(i => new
            {
                i.Id,
                i.WorkspaceId,
                i.Title,
                i.Subject,
                i.Signal,
                i.Source,
                i.Tags,
                i.Confidence,
                observedAtUtc = i.ObservedAtUtc,
                notes = string.IsNullOrWhiteSpace(i.Notes) ? null : (i.Notes!.Length > 800 ? i.Notes.Substring(0, 800) : i.Notes)
            })
        };

        var systemPrompt =
            "You are a strategic analyst for a business War Room. " +
            "You must output STRICT JSON only. " +
            "Goal: create a short, actionable intel brief (directed telescope). " +
            "Separate signal from noise. Highlight contradictions and what to verify next. " +
            "If confidence is low, say so and propose verification steps. " +
            "Schema:\n" +
            "{\n" +
            "  \"title\": string,\n" +
            "  \"executiveSummary\": string,\n" +
            "  \"keySignals\": [string],\n" +
            "  \"contradictionsOrConflicts\": [string],\n" +
            "  \"missingIntelQuestions\": [string],\n" +
            "  \"recommendedNextMoves\": [string],\n" +
            "  \"overallConfidence1to5\": number\n" +
            "}";

        var inputJson = JsonSerializer.Serialize(payload);

        var startedUtc = DateTime.UtcNow;
        var result = await _ai.CreateJsonInsightsAsync(systemPrompt, inputJson, ct);
        var durationMs = (int)(DateTime.UtcNow - startedUtc).TotalMilliseconds;

        // FIX: Match your existing DecisionTraceService.LogAsync overload (no 'operation', etc.)
        try
        {
            await _trace.LogAsync(
                ownerId: ownerId,
                workspaceId: workspaceId,
                feature: "WarRoom.CreateIntelBrief",
                input: new
                {
                    systemPrompt,
                    inputJson,
                    durationMs,
                    contextPack = JsonDocument.Parse(ctx).RootElement,
                    intelCount = items.Count
                },
                outputJson: result,
                entityType: "WarIntel",
                entityId: null,
                entityTitle: $"IntelBrief ({items.Count} items)",
                ct: ct
            );
        }
        catch
        {
            // Never block user flows.
        }

        return JsonDocument.Parse(result);
    }

    public async Task<JsonDocument> RedTeamPlanAsync(string ownerId, int planId, CancellationToken ct)
    {
        var plan = await _db.WarPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == planId, ct);

        if (plan == null)
            return JsonDocument.Parse("{\"error\":\"Plan not found.\"}");

        var ctx = await _contextPack.BuildAsync(ownerId, plan.WorkspaceId, ct);

        var intel = await _db.WarIntel
            .AsNoTracking()
            .Where(x => x.OwnerId == ownerId && (plan.WorkspaceId == null || x.WorkspaceId == plan.WorkspaceId))
            .OrderByDescending(x => x.ObservedAtUtc ?? x.CreatedAtUtc)
            .Take(12)
            .Select(i => new { i.Title, i.Subject, i.Signal, i.Tags, i.Confidence })
            .ToListAsync(ct);

        var payload = new
        {
            intent = "red_team_plan",
            nowUtc = DateTime.UtcNow,
            contextPack = JsonDocument.Parse(ctx).RootElement,
            plan = new
            {
                plan.Id,
                plan.WorkspaceId,
                plan.Name,
                plan.Status,
                plan.Objective,
                plan.Approach,
                plan.Assumptions,
                plan.Risks,
                plan.Contingencies,
                plan.StartAtUtc,
                plan.EndAtUtc
            },
            contextIntel = intel
        };

        var systemPrompt =
            "You are a skeptical red-team strategist. Output STRICT JSON only. " +
            "Your job: stress-test the plan for: unclear objective, weak assumptions, missing counter-moves, " +
            "overconfidence, poor sequencing/tempo, and lack of contingencies. " +
            "Call out contradictions against the provided intel. " +
            "Also propose 2-3 alternative approaches (indirect moves) and 3 verification steps. " +
            "Schema:\n" +
            "{\n" +
            "  \"summary\": string,\n" +
            "  \"criticalGaps\": [string],\n" +
            "  \"assumptionRisks\": [string],\n" +
            "  \"likelyOpponentResponses\": [string],\n" +
            "  \"recommendedAdjustments\": [string],\n" +
            "  \"alternativeApproaches\": [string],\n" +
            "  \"verificationSteps\": [string],\n" +
            "  \"confidenceInPlan1to5\": number\n" +
            "}";

        var inputJson = JsonSerializer.Serialize(payload);

        var startedUtc = DateTime.UtcNow;
        var result = await _ai.CreateJsonInsightsAsync(systemPrompt, inputJson, ct);
        var durationMs = (int)(DateTime.UtcNow - startedUtc).TotalMilliseconds;

        // FIX: Match your existing DecisionTraceService.LogAsync overload (no 'operation', etc.)
        try
        {
            await _trace.LogAsync(
                ownerId: ownerId,
                workspaceId: plan.WorkspaceId,
                feature: "WarRoom.RedTeamPlan",
                input: new
                {
                    systemPrompt,
                    inputJson,
                    durationMs,
                    contextPack = JsonDocument.Parse(ctx).RootElement,
                    planId = plan.Id,
                    planName = plan.Name
                },
                outputJson: result,
                entityType: "WarPlan",
                entityId: plan.Id,
                entityTitle: plan.Name,
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
