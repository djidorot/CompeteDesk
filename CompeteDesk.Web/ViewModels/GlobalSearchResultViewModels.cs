using System.Collections.Generic;

namespace CompeteDesk.ViewModels.Search;

public sealed class GlobalSearchResponseViewModel
{
    public string Query { get; set; } = string.Empty;
    public string Entity { get; set; } = "all";
    public string? Category { get; set; }
    public string? Priority { get; set; }
    public string? Status { get; set; }

    public IReadOnlyList<string> Categories { get; set; } = new List<string>();
    public IReadOnlyList<string> PriorityOptions { get; set; } = new[] { "High", "Medium", "Low" };
    public IReadOnlyList<string> StatusOptions { get; set; } = new[] { "Active", "Archived" };

    public int TotalCount { get; set; }
    public int StrategyCount { get; set; }
    public int WorkspaceCount { get; set; }
    public int UserCount { get; set; }

    public bool CanSearchUsers { get; set; }

    public IReadOnlyList<GlobalSearchItemViewModel> Strategies { get; set; } = new List<GlobalSearchItemViewModel>();
    public IReadOnlyList<GlobalSearchItemViewModel> Workspaces { get; set; } = new List<GlobalSearchItemViewModel>();
    public IReadOnlyList<GlobalSearchItemViewModel> Users { get; set; } = new List<GlobalSearchItemViewModel>();
}

public sealed class GlobalSearchItemViewModel
{
    public string EntityType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Meta { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
