// FILE: CompeteDesk/Services/Ai/AiContextPackBuilder.cs
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;

namespace CompeteDesk.Services.Ai;

public sealed class AiContextPackBuilder
{
    private readonly ApplicationDbContext _db;

    public AiContextPackBuilder(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Builds a structured context pack to differentiate CompeteDesk from generic chat tools.
    /// This is intentionally small and safe to include in AI payloads.
    /// </summary>
    public async Task<string> BuildAsync(string ownerId, int? workspaceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return "{}";

        object? workspace = null;
        object[] strategies = Array.Empty<object>();
        object[] recentIntel = Array.Empty<object>();

        if (workspaceId.HasValue)
        {
            var ws = await _db.Workspaces
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == workspaceId.Value && x.OwnerId == ownerId, ct);

            if (ws != null)
            {
                workspace = new
                {
                    ws.Id,
                    ws.Name,
                    ws.Description,
                    ws.BusinessType,
                    ws.Country,
                    ws.BusinessProfileUpdatedAtUtc,
                    ws.CreatedAtUtc,
                    ws.UpdatedAtUtc
                };

                strategies = await _db.Strategies
                    .AsNoTracking()
                    .Where(s => s.OwnerId == ownerId && s.WorkspaceId == ws.Id && s.Status == "Active")
                    .OrderByDescending(s => s.UpdatedAtUtc ?? s.CreatedAtUtc)
                    .Take(8)
                    .Select(s => new
                    {
                        s.Id,
                        name = s.Name,
                        category = s.Category,
                        summary = s.Summary,
                        status = s.Status,
                        priority = s.Priority
                    })
                    .Cast<object>()
                    .ToArrayAsync(ct);

                recentIntel = await _db.WarIntel
                    .AsNoTracking()
                    .Where(i => i.OwnerId == ownerId && i.WorkspaceId == ws.Id)
                    .OrderByDescending(i => i.ObservedAtUtc ?? i.CreatedAtUtc)
                    .Take(8)
                    .Select(i => new
                    {
                        i.Id,
                        i.Title,
                        i.Subject,
                        i.Signal,
                        i.Tags,
                        i.Confidence,
                        i.Source,
                        i.ObservedAtUtc,
                        i.CreatedAtUtc
                    })
                    .Cast<object>()
                    .ToArrayAsync(ct);
            }
        }

        var pack = new
        {
            generatedAtUtc = DateTime.UtcNow,
            workspace,
            activeStrategies = strategies,
            recentIntel,
            note = "Context pack is persisted workspace state (not a chat)."
        };

        return JsonSerializer.Serialize(pack, new JsonSerializerOptions { WriteIndented = true });
    }
}
