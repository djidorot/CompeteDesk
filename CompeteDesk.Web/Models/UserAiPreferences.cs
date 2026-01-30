using System;

namespace CompeteDesk.Models
{
    public class UserAiPreferences
    {
        public int Id { get; set; }

        /// <summary>
        /// ASP.NET Identity User Id (string).
        /// </summary>
        public string UserId { get; set; } = "";

        // Display preferences
        public string Verbosity { get; set; } = "Balanced";   // Short | Balanced | Detailed
        public string Tone { get; set; } = "Analytical";      // Executive | Analytical | Tactical

        // Behavior toggles
        public bool AutoDraftPlans { get; set; } = true;
        public bool AutoSummaries { get; set; } = true;
        public bool AutoRecommendations { get; set; } = true;

        /// <summary>
        /// Whether to store DecisionTrace records for AI calls.
        /// </summary>
        public bool StoreDecisionTraces { get; set; } = true;

        public DateTime? CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
