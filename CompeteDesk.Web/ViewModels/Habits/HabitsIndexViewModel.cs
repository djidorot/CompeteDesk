using System.Collections.Generic;

namespace CompeteDesk.ViewModels.Habits;

public class HabitsIndexViewModel
{
    public string Query { get; set; } = "";
    public int? WorkspaceId { get; set; }
    public int? StrategyId { get; set; }
    public string Frequency { get; set; } = "";

    public List<(int Id, string Name)> Workspaces { get; set; } = new();
    public List<(int Id, string Name)> Strategies { get; set; } = new();

    public List<HabitListItemViewModel> Habits { get; set; } = new();

    // AI integration
    public bool IsAiConfigured { get; set; }
}
