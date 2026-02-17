using System;
using System.Collections.Generic;

namespace CompeteDesk.ViewModels.AiHistory;

public sealed class AiHistoryIndexViewModel
{
    public int? WorkspaceId { get; set; }
    public string? Feature { get; set; }
    public string? Q { get; set; }

    // Paging
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }

    public List<AiHistoryRow> Items { get; set; } = new();
}

public sealed class AiHistoryRow
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Feature { get; set; } = string.Empty;
    public int? WorkspaceId { get; set; }
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string? EntityTitle { get; set; }
}
