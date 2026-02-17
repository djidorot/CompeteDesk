using System.Collections.Generic;

namespace CompeteDesk.ViewModels.Common;

public sealed class PagerViewModel
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }

    public string Action { get; set; } = "Index";
    public string Controller { get; set; } = "";

    /// <summary>
    /// Extra route values to keep filters in pagination links.
    /// </summary>
    public Dictionary<string, object?> RouteValues { get; set; } = new();

    /// <summary>
    /// If true, links will include partial=1 and the JS loader will fetch/replace content.
    /// </summary>
    public bool Async { get; set; } = true;
}
