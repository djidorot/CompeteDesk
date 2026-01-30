using CompeteDesk.Models;

namespace CompeteDesk.ViewModels.Strategies;

public class StrategyDetailsViewModel
{
    public Strategy Strategy { get; set; } = new();

    public StrategyCommandHeaderViewModel Header { get; set; } = new();

    // Optional diagnostics for later UI expansion
    public int TotalActions { get; set; }
    public int DoneActions { get; set; }
}
