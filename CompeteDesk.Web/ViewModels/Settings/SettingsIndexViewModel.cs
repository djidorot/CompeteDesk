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

        // Subscription + quota
        public string SubscriptionTier { get; set; } = "Free";
        public string SubscriptionStatus { get; set; } = "Active";
        public int MonthlyAiLimit { get; set; }
        public int MonthlyAiUsed { get; set; }
        public int MonthlyExportLimit { get; set; }
        public int MonthlyExportUsed { get; set; }
        public int WorkspaceLimit { get; set; }
        public int WorkspaceUsed { get; set; }
        public string QuotaPeriodKey { get; set; } = "";
        public string UpgradeTier { get; set; } = "Pro";
        public string UpgradePaymentMethod { get; set; } = "QR";
        public string UpgradeReferenceNumber { get; set; } = "";
        public string? UpgradeNotes { get; set; }
        public bool HasPendingPaymentRequest { get; set; }

        // Reset demo data confirmation
        public string? ResetConfirm { get; set; }
    }
}
