using System.Collections.Generic;
using CompeteDesk.Models;

namespace CompeteDesk.ViewModels.WebsiteAnalysis;

public sealed class WebsiteAnalysisIndexViewModel
{
    public string Url { get; set; } = "";
    public int? WorkspaceId { get; set; }

    public WebsiteAnalysisReport? Latest { get; set; }
    public List<WebsiteAnalysisReport> Recent { get; set; } = new();
    public string? Error { get; set; }
}
