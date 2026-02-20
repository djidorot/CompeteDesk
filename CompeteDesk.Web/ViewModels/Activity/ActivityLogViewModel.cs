using System;
using System.Collections.Generic;

namespace CompeteDesk.ViewModels.Activity;

public class ActivityLogViewModel
{
    public bool IsAdmin { get; set; }
    public string ScopeLabel { get; set; } = "My Activity";

    // Filters
    public string? Q { get; set; }
    public string? ActionFilter { get; set; }
    public string? EntityTypeFilter { get; set; }
    public string? ActorFilter { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }

    // Filter options
    public List<string> ActionOptions { get; set; } = new();
    public List<string> EntityTypeOptions { get; set; } = new();

    // Paging
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }

    public List<ActivityLogItem> Items { get; set; } = new();
}

public class ActivityLogItem
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? ActorEmail { get; set; }
    public string Action { get; set; } = "";
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Summary { get; set; }
}
