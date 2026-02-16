using System;
using System.Collections.Generic;

namespace CompeteDesk.ViewModels.AiInsights;

public sealed class AiInsightsViewModel
{
    public int? WorkspaceId { get; set; }
    public string? WorkspaceName { get; set; }

    public AiPerformanceBlock Performance { get; set; } = new();
    public List<AiWeakAreaItem> WeakAreas { get; set; } = new();
    public List<AiRecommendationItem> Recommendations { get; set; } = new();
    public List<AiFeatureUsageRow> FeatureUsage { get; set; } = new();
}

public sealed class AiPerformanceBlock
{
    public int TracesLast7Days { get; set; }
    public int TracesLast30Days { get; set; }
    public DateTime? LastTraceAtUtc { get; set; }
    public int StrategiesWithPlaybook { get; set; }
    public int StrategiesTotal { get; set; }
    public int ActionsOpen { get; set; }
    public int ActionsDoneLast14Days { get; set; }
}

public sealed class AiWeakAreaItem
{
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string? Href { get; set; }
}

public sealed class AiRecommendationItem
{
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string? Href { get; set; }
}

public sealed class AiFeatureUsageRow
{
    public string Feature { get; set; } = string.Empty;
    public int Count30Days { get; set; }
}
