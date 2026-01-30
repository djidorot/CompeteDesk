using CompeteDesk.ViewModels.Dashboard;

namespace CompeteDesk.ViewModels.BusinessAnalysis
{
    public sealed class BusinessAnalysisPageViewModel
    {
        public int WorkspaceId { get; set; }
        public string WorkspaceName { get; set; } = "";

        public bool NeedsWorkspace { get; set; }
        public bool NeedsBusinessProfile { get; set; }
        public string? BusinessType { get; set; }
        public string? Country { get; set; }

        public BusinessAnalysisViewModel? Latest { get; set; }
    }
}
