using CompeteDesk.Models;

namespace CompeteDesk.ViewModels.Admin;

public class AdminDashboardViewModel
{
    public int Users { get; set; }
    public int Workspaces { get; set; }
    public int Strategies { get; set; }
    public int Actions { get; set; }
    public int WarIntel { get; set; }
    public int WarPlans { get; set; }
    public int WebsiteReports { get; set; }
    public int BusinessReports { get; set; }
    public int DecisionTraces { get; set; }

    public List<RecentUserItem> RecentUsers { get; set; } = new();
    public List<DecisionTrace> RecentDecisionTraces { get; set; } = new();
}

public class RecentUserItem
{
    public string Id { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? UserName { get; set; }
}
