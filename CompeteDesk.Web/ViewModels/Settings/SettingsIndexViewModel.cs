namespace CompeteDesk.ViewModels.Settings
{
    public class SettingsIndexViewModel
    {
        // Profile
        public string Email { get; set; } = "";
        public string DisplayName { get; set; } = "";

        // AI Preferences
        public string Verbosity { get; set; } = "Balanced";   // Short | Balanced | Detailed
        public string Tone { get; set; } = "Analytical";      // Executive | Analytical | Tactical

        public bool AutoDraftPlans { get; set; } = true;
        public bool AutoSummaries { get; set; } = true;
        public bool AutoRecommendations { get; set; } = true;
        public bool StoreDecisionTraces { get; set; } = true;

        // Data & Analytics Controls
        public int RetentionDays { get; set; } = 90; // 30 | 90 | 365
        public string ExportFormat { get; set; } = "json"; // csv | json

        // Reset demo data confirmation
        public string? ResetConfirm { get; set; }
    }
}
