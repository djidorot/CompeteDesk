using System;

namespace CompeteDesk.ViewModels.Habits;

public class HabitListItemViewModel
{
    public int Id { get; set; }
    public int WorkspaceId { get; set; }
    public string WorkspaceName { get; set; } = "";
    public int? StrategyId { get; set; }
    public string? StrategyName { get; set; }

    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Frequency { get; set; } = "Daily";
    public int TargetCount { get; set; }
    public bool IsActive { get; set; }

    // Progress
    public int PeriodCount { get; set; }
    public int TodayCount { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
}
