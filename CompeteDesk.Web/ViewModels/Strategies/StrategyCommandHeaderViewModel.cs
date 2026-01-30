namespace CompeteDesk.ViewModels.Strategies;

/// <summary>
/// ViewModel for the Strategy Command Header (Hero Section).
/// Provides immediate “battle readiness” awareness at a glance.
/// </summary>
public class StrategyCommandHeaderViewModel
{
    // Left panel
    public string WorkspaceName { get; set; } = "No Workspace";
    public string StrategyType { get; set; } = "Growth";
    public int ActiveStrategyCount { get; set; }

    // Right panel
    public int StrategyScore { get; set; }

    /// <summary>
    /// Values: OnTrack | AtRisk | OffTrack
    /// </summary>
    public string StatusKey { get; set; } = "AtRisk";

    public string StatusLabel
        => StatusKey switch
        {
            "OnTrack" => "🟢 On Track",
            "OffTrack" => "🔴 Off Track",
            _ => "🟡 At Risk"
        };
}
