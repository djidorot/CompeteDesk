using System;

namespace CompeteDesk.Models;

/// <summary>
/// Persistence + traceability for AI-generated outputs.
/// This is the core "Why not just ask ChatGPT?" differentiator:
/// - Context: store structured context pack used for generation
/// - Structure: store feature + entity links
/// - Persistence/History: searchable list over time
/// - Decision traceability: store system prompt + user payload + output
/// </summary>
public sealed class DecisionTrace
{
    public int Id { get; set; }

    // Multi-tenant owner scope (Identity user id)
    public string OwnerId { get; set; } = string.Empty;

    // Optional workspace scope
    public int? WorkspaceId { get; set; }

    // Where did this come from? (e.g. "WarRoom.RedTeamPlan", "BusinessAnalysis.Onboarding")
    public string Feature { get; set; } = string.Empty;

    // Optional link to a domain entity that the AI call was about.
    // Example: EntityType="WarPlan", EntityId=123
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string? EntityTitle { get; set; }

    // A stable correlation id for client-side linking/debugging
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");

    // Stored as TEXT (JSON) so we can evolve without schema churn.
    public string? InputJson { get; set; }   // includes system prompt, user payload, context pack, etc.
    public string? OutputJson { get; set; }  // the generated JSON response (or structured content)

    // Model metadata for future audits
    public string? AiProvider { get; set; } = "OpenAI";
    public string? Model { get; set; }
    public double? Temperature { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
