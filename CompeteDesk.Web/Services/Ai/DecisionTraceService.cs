using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Models;

namespace CompeteDesk.Services.Ai;

/// <summary>
/// Centralized persistence for AI calls (the "history + traceability" layer).
/// </summary>
public sealed class DecisionTraceService
{
    private readonly ApplicationDbContext _db;

    public DecisionTraceService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<int> LogAsync(
        string ownerId,
        int? workspaceId,
        string feature,
        object input,
        string outputJson,
        string? entityType = null,
        int? entityId = null,
        string? entityTitle = null,
        string? aiProvider = "OpenAI",
        string? model = null,
        double? temperature = null,
        string? correlationId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("ownerId required.", nameof(ownerId));
        if (string.IsNullOrWhiteSpace(feature)) throw new ArgumentException("feature required.", nameof(feature));

        var inputJson = input is string s
            ? s
            : JsonSerializer.Serialize(input, new JsonSerializerOptions { WriteIndented = false });

        var trace = new DecisionTrace
        {
            OwnerId = ownerId,
            WorkspaceId = workspaceId,
            Feature = feature,
            EntityType = entityType,
            EntityId = entityId,
            EntityTitle = entityTitle,
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId!,
            InputJson = inputJson,
            OutputJson = outputJson,
            AiProvider = aiProvider,
            Model = model,
            Temperature = temperature,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.DecisionTraces.Add(trace);
        await _db.SaveChangesAsync(ct);
        return trace.Id;
    }

    public Task<DecisionTrace?> GetAsync(string ownerId, int id, CancellationToken ct)
        => _db.DecisionTraces.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == id, ct);
}
