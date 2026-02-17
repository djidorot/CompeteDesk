using System;
using System.Collections.Generic;

namespace CompeteDesk.ViewModels.Activity;

public class EntityHistoryViewModel
{
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Title { get; set; } = "Change History";

    // Paging
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }

    public List<EntityHistoryItem> Items { get; set; } = new();
}

public class EntityHistoryItem
{
    public int Id { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public string Action { get; set; } = "";
    public string? ActorEmail { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}
