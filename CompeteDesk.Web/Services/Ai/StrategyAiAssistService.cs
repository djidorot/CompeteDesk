using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Services.OpenAI;

namespace CompeteDesk.Services.Ai;

/// <summary>
/// Lightweight per-Strategy "AI Assist" helpers.
/// This complements the existing AI Competitive Playbook generator.
///
/// Outputs are persisted in DecisionTraces for auditability.
/// </summary>
public sealed class StrategyAiAssistService
{
    private readonly ApplicationDbContext _db;
    private readonly OpenAiChatClient _openAi;
    private readonly AiContextPackBuilder _ctx;
    private readonly DecisionTraceService _trace;

    public StrategyAiAssistService(
        ApplicationDbContext db,
        OpenAiChatClient openAi,
        AiContextPackBuilder ctx,
        DecisionTraceService trace)
    {
        _db = db;
        _openAi = openAi;
        _ctx = ctx;
        _trace = trace;
    }

    public bool IsConfigured => _openAi.IsConfigured;

    public enum AssistKind
    {
        Swot,
        StudySummary,
        Quiz,
        Improvements
    }

    public sealed record AssistResult(
        AssistKind Kind,
        string OutputJson,
        int TraceId);

    public async Task<AssistResult> GenerateAsync(
        string ownerId,
        int strategyId,
        AssistKind kind,
        string? userGoal,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ArgumentException("ownerId required", nameof(ownerId));

        var s = await _db.Strategies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == strategyId && x.OwnerId == ownerId, ct);

        if (s is null)
            throw new InvalidOperationException("Strategy not found.");

        var contextPack = await _ctx.BuildAsync(ownerId, s.WorkspaceId, ct);

        var system = BuildSystemPrompt(kind);

        var payload = new
        {
            kind = kind.ToString(),
            userGoal = string.IsNullOrWhiteSpace(userGoal) ? null : userGoal.Trim(),
            strategy = new
            {
                s.Id,
                s.WorkspaceId,
                s.Name,
                s.Category,
                s.StrategyType,
                s.SourceBook,
                s.CorePrinciple,
                s.Summary,
                s.Priority,
                s.Status,
            },
            contextPack = JsonDocument.Parse(contextPack).RootElement
        };

        var outputJson = await _openAi.CreateJsonInsightsAsync(
            system,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
            ct);

        // Validate it's JSON (defensive). If invalid, wrap.
        outputJson = NormalizeJson(outputJson);

        var feature = kind switch
        {
            AssistKind.Swot => "Strategy.Assist.SWOT",
            AssistKind.StudySummary => "Strategy.Assist.StudySummary",
            AssistKind.Quiz => "Strategy.Assist.Quiz",
            AssistKind.Improvements => "Strategy.Assist.Improvements",
            _ => "Strategy.Assist"
        };

        var traceId = await _trace.LogAsync(
            ownerId: ownerId,
            workspaceId: s.WorkspaceId,
            feature: feature,
            input: payload,
            outputJson: outputJson,
            entityType: "Strategy",
            entityId: s.Id,
            entityTitle: s.Name,
            aiProvider: "OpenAI",
            model: null,
            temperature: null,
            correlationId: null,
            ct: ct);

        return new AssistResult(kind, outputJson, traceId);
    }

    private static string BuildSystemPrompt(AssistKind kind)
    {
        // IMPORTANT: Keep this safe and business-friendly.
        // Output must be a JSON object (response_format=json_object is enforced).
        var baseRules = "You are CompeteDesk AI Assist. Produce helpful, business-safe outputs. " +
                        "Return ONLY valid JSON. No markdown. No extra keys not requested.";

        return kind switch
        {
            AssistKind.Swot => baseRules + "\n\n" +
                              "Task: Generate a SWOT analysis for the provided strategy and workspace context. " +
                              "Return JSON with keys: strengths (array of strings), weaknesses (array), opportunities (array), threats (array), " +
                              "notes (string, short), and nextSteps (array of {title, detail}).",

            AssistKind.StudySummary => baseRules + "\n\n" +
                                       "Task: Create a study summary to help the user learn/apply this strategy. " +
                                       "Return JSON with keys: oneLine (string), keyIdeas (array of strings), whenToUse (array), whenNotToUse (array), " +
                                       "examples (array of {scenario, whatToDo}), and quickChecklist (array of strings).",

            AssistKind.Quiz => baseRules + "\n\n" +
                             "Task: Generate a short quiz to test understanding of this strategy. " +
                             "Return JSON with keys: title (string), questions (array of objects). Each question: {type, prompt, choices, answer, explanation}. " +
                             "Rules: 8 questions total. Mix types: mcq, true_false, short. For mcq include 4 choices. Keep answers concise.",

            AssistKind.Improvements => baseRules + "\n\n" +
                                     "Task: Suggest improvements and execution ideas to apply this strategy in the user's context. " +
                                     "Return JSON with keys: improvements (array of {title, why, how}), risks (array of {risk, mitigation}), " +
                                     "metrics (array of {name, target, why}), and focusAreas (array of strings).",

            _ => baseRules
        };
    }

    private static string NormalizeJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "{}";

        raw = raw.Trim();

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.GetRawText();
        }
        catch
        {
            // Wrap non-JSON responses to keep clients stable.
            var wrapped = new { error = "Model returned invalid JSON.", raw };
            return JsonSerializer.Serialize(wrapped);
        }
    }
}
