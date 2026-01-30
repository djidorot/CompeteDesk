// FILE: CompeteDesk/Services/Habits/HabitsAiService.cs
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Services.Ai;
using CompeteDesk.Services.OpenAI;

namespace CompeteDesk.Services.Habits;

/// <summary>
/// AI helper for Habits: suggests daily/weekly habits tied to a Workspace (+ optional Strategy).
/// Output is STRICT JSON so the UI can render it safely.
/// </summary>
public sealed class HabitsAiService
{
    private readonly ApplicationDbContext _db;
    private readonly OpenAiChatClient _ai;
    private readonly DecisionTraceService _trace;
    private readonly AiContextPackBuilder _contextPack;

    public HabitsAiService(
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

    public async Task<JsonDocument> SuggestAsync(
        string ownerId,
        int workspaceId,
        int? strategyId,
        string? goal,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return JsonDocument.Parse("{\"error\":\"Not authenticated.\"}");

        var ws = await _db.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == workspaceId, ct);

        if (ws == null)
            return JsonDocument.Parse("{\"error\":\"Workspace not found.\"}");

        var strategy = strategyId.HasValue
            ? await _db.Strategies.AsNoTracking().FirstOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == strategyId.Value, ct)
            : null;

        var ctx = await _contextPack.BuildAsync(ownerId, workspaceId, ct);

        // Give the model a small view of existing habits to avoid duplicates.
        var existing = await _db.Habits.AsNoTracking()
            .Where(x => x.OwnerId == ownerId && x.WorkspaceId == workspaceId && (strategyId == null || x.StrategyId == strategyId))
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Title)
            .Select(x => new { x.Title, x.Frequency, x.TargetCount, x.IsActive })
            .Take(40)
            .ToListAsync(ct);

        var payload = new
        {
            intent = "habits_suggest",
            nowUtc = DateTime.UtcNow,
            workspace = new
            {
                ws.Id,
                ws.Name,
                ws.BusinessType,
                ws.Country,
                ws.Description
            },
            strategy = strategy == null ? null : new
            {
                strategy.Id,
                strategy.Name,
                strategy.CorePrinciple,
                strategy.Summary,
                strategy.Category
            },
            userGoal = string.IsNullOrWhiteSpace(goal) ? null : goal.Trim(),
            existingHabits = existing,
            contextPack = JsonDocument.Parse(ctx).RootElement
        };

        var systemPrompt =
            "You are a strategy + behavior design coach. Output STRICT JSON only (no markdown). " +
            "Propose 5-8 habits that are SMALL, repeatable, and measurable, tied to the workspace and (if provided) strategy. " +
            "Mix daily and weekly habits. Avoid duplicates with existingHabits. " +
            "Default targets: daily=1, weekly=1-3 depending on habit. " +
            "Each habit should include: cue, routine, reward, and a rationale linking to the strategy. " +
            "Schema:\n" +
            "{\n" +
            "  \"suggestions\": [\n" +
            "    {\n" +
            "      \"title\": string,\n" +
            "      \"description\": string,\n" +
            "      \"frequency\": \"Daily\" | \"Weekly\",\n" +
            "      \"targetCount\": number,\n" +
            "      \"cue\": string,\n" +
            "      \"routine\": string,\n" +
            "      \"reward\": string,\n" +
            "      \"rationale\": string\n" +
            "    }\n" +
            "  ],\n" +
            "  \"notes\": [string]\n" +
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
                feature: "Habits.Suggest",
                input: new { systemPrompt, inputJson, durationMs },
                outputJson: result,
                entityType: "Habit",
                entityId: null,
                entityTitle: $"Habit suggestions ({ws.Name})",
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
