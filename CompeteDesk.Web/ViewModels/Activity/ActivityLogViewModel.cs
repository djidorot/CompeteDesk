using System;
using System.Collections.Generic;

namespace CompeteDesk.ViewModels.Activity;

public class ActivityLogViewModel
{
    public bool IsAdmin { get; set; }
    public string ScopeLabel { get; set; } = "My Activity";
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
