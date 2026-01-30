using System;
using System.Collections.Generic;

namespace CompeteDesk.ViewModels.StrategyCopilot;

public sealed class StrategyCopilotIndexViewModel
{
    public int? WorkspaceId { get; set; }
    public List<StrategyCopilotWorkspaceOption> Workspaces { get; set; } = new();
    public List<StrategyCopilotStrategyOption> Strategies { get; set; } = new();
    public List<StrategyCopilotIntelOption> Intel { get; set; } = new();
}

public sealed class StrategyCopilotWorkspaceOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class StrategyCopilotStrategyOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
}

public sealed class StrategyCopilotIntelOption
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public int Confidence { get; set; }
    public DateTime ObservedAtUtc { get; set; }
}
