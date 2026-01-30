using System;
using System.Collections.Generic;

namespace CompeteDesk.ViewModels.Dashboard
{
    public sealed class BusinessAnalysisViewModel
    {
        public int WorkspaceId { get; set; }
        public string WorkspaceName { get; set; } = "";

        public string BusinessType { get; set; } = "";
        public string Country { get; set; } = "";

        public DateTime CreatedAtUtc { get; set; }

        // SWOT
        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
        public List<string> Opportunities { get; set; } = new();
        public List<string> Threats { get; set; } = new();

        // Porter’s Five Forces (your business)
        public ForceVm Rivalry { get; set; } = new();
        public ForceVm NewEntrants { get; set; } = new();
        public ForceVm Substitutes { get; set; } = new();
        public ForceVm SupplierPower { get; set; } = new();
        public ForceVm BuyerPower { get; set; } = new();

        // Competitors
        public List<CompetitorVm> Competitors { get; set; } = new();
    }

    public sealed class ForceVm
    {
        /// <summary>
        /// 1 (Low) to 5 (High)
        /// </summary>
        public int Score { get; set; }
        public string Notes { get; set; } = "";

        public string Level
        {
            get
            {
                return Score switch
                {
                    <= 1 => "Low",
                    2 => "Low-Med",
                    3 => "Medium",
                    4 => "Med-High",
                    _ => "High"
                };
            }
        }
    }

    public sealed class CompetitorVm
    {
        public string Name { get; set; } = "";
        public string? Website { get; set; }
        public string? Summary { get; set; }
        public string? WhyRelevant { get; set; }

        public ForceVm Rivalry { get; set; } = new();
        public ForceVm NewEntrants { get; set; } = new();
        public ForceVm Substitutes { get; set; } = new();
        public ForceVm SupplierPower { get; set; } = new();
        public ForceVm BuyerPower { get; set; } = new();
    }
}