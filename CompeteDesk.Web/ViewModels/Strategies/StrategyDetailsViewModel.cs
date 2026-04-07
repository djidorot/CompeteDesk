using System;
using System.Collections.Generic;
using CompeteDesk.Models;

namespace CompeteDesk.ViewModels.Strategies;

public class StrategyDetailsViewModel
{
    public Strategy Strategy { get; set; } = new();

    public StrategyCommandHeaderViewModel Header { get; set; } = new();

    // Optional diagnostics for later UI expansion
    public int TotalActions { get; set; }
    public int DoneActions { get; set; }

    public List<StrategyCommentItem> Comments { get; set; } = new();
}

public class StrategyCommentItem
{
    public int Id { get; set; }
    public string? AuthorEmail { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
