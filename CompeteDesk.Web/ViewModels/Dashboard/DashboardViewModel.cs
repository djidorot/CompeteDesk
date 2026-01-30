using System;
using System.Collections.Generic;
using System.Linq;

namespace CompeteDesk.ViewModels.Dashboard
{
    public sealed class DashboardViewModel
    {
        // Workspace context
        public int WorkspaceId { get; set; }
        public string WorkspaceName { get; set; } = "My Workspace";

        // When true, the user hasn't created a workspace yet.
        // The Dashboard should still render and guide them to create one.
        public bool NeedsWorkspace { get; set; }

        // User context
        public string UserDisplayName { get; set; } = "User";

        // Business profile (used by Business Analysis onboarding)
        public string? BusinessType { get; set; }
        public string? Country { get; set; }
        public bool NeedsBusinessProfile { get; set; }

        // Business Analysis (AI)
        public BusinessAnalysisViewModel? BusinessAnalysis { get; set; }

        // Feature Overview tiles (for the "Overview" page section)
        public List<FeatureTileItem> FeatureTiles { get; set; } = new();

        // Overview summary (counts + quick access to each module)
        public List<OverviewSummaryItem> OverviewSummary { get; set; } = new();

        // Command header
        public string StrategyMode { get; set; } = "Growth"; // Offensive / Defensive / Growth / Stability
        public int ActiveStrategiesCount { get; set; }
        public int StrategyScore { get; set; } // 0-100
        public string HealthStatus { get; set; } = "On Track"; // On Track / At Risk / Off Track
        public string WeeklyFocus { get; set; } = "Make the right actions easy — and automatic.";

        // Weekly review (reflection)
        public string WeeklyReviewHighlight { get; set; } = "";
        public string WeeklyReviewFailure { get; set; } = "";
        public string WeeklyReviewAdjustment { get; set; } = "";

        // Today's critical actions
        public List<TodayActionItem> TodayActions { get; set; } = new();

        // Habit systems
        public List<HabitSystemItem> HabitSystems { get; set; } = new();

        // Back-compat for older dashboard view
        public List<HabitSystemItem> Habits
        {
            get => HabitSystems;
            set => HabitSystems = value ?? new();
        }

        // Strategies summary cards
        public List<StrategyCardItem> StrategyCards { get; set; } = new();

        // Back-compat for older dashboard view
        public List<StrategyCardItem> ActiveStrategies
        {
            get => StrategyCards;
            set => StrategyCards = value ?? new();
        }

        // Metrics/KPIs
        public List<MetricKpiItem> Kpis { get; set; } = new();

        // Back-compat for older dashboard view
        public List<MetricKpiItem> KeyMetrics
        {
            get => Kpis;
            set => Kpis = value ?? new();
        }

        public static DashboardViewModel Sample(string userDisplayName)
        {
            var vm = new DashboardViewModel
            {
                WorkspaceId = 1,
                WorkspaceName = "CompeteDesk Demo Workspace",
                UserDisplayName = string.IsNullOrWhiteSpace(userDisplayName) ? "Strategist" : userDisplayName,
                StrategyMode = "Growth",
                ActiveStrategiesCount = 4,
                StrategyScore = 82,
                HealthStatus = "On Track",
                WeeklyFocus = "Make the right actions easy — and automatic.",
                BusinessType = null,
                Country = null,
                NeedsBusinessProfile = true
            };

            vm.FeatureTiles.AddRange(new[]
            {
                new FeatureTileItem { Title = "Workspaces", Description = "Create and manage strategic workspaces.", Href = "/Workspaces" },
                new FeatureTileItem { Title = "Strategies", Description = "Build playbooks and strategic moves.", Href = "/Strategies" },
                new FeatureTileItem { Title = "Actions", Description = "Track critical actions and execution.", Href = "/Actions" },
                new FeatureTileItem { Title = "Habits", Description = "Turn strategy into repeatable systems.", Href = "/Habits" },
                new FeatureTileItem { Title = "Metrics", Description = "Measure what matters with KPIs.", Href = "/Metrics" },
                new FeatureTileItem { Title = "Website Analysis", Description = "Analyze a website and generate insights.", Href = "/WebsiteAnalysis" },
                new FeatureTileItem { Title = "War Room", Description = "Capture intel and plans for competition.", Href = "/WarRoom" },
                new FeatureTileItem { Title = "Business Analysis (AI)", Description = "SWOT + Porter’s Five Forces + competitors.", Href = "/BusinessAnalysis" }
            });

            // Default overview summary (will be replaced with real counts by DashboardController)
            vm.OverviewSummary.AddRange(new[]
            {
                new OverviewSummaryItem { Title = "Strategies", Href = "/Strategies", Count = 4, Subtitle = "Playbooks and strategic moves", Badge = "Active" },
                new OverviewSummaryItem { Title = "Actions", Href = "/Actions", Count = 9, Subtitle = "Execution and to-dos", Badge = "Today" },
                new OverviewSummaryItem { Title = "Website Analysis", Href = "/WebsiteAnalysis", Count = 2, Subtitle = "Website insight reports", Badge = "AI" },
                new OverviewSummaryItem { Title = "War Room", Href = "/WarRoom", Count = 3, Subtitle = "Intel + plans", Badge = "Ops" },
                new OverviewSummaryItem { Title = "Habits", Href = "/Habits", Count = 0, Subtitle = "Systems & routines", Badge = "Coming soon", Disabled = true },
                new OverviewSummaryItem { Title = "Metrics", Href = "/Metrics", Count = 0, Subtitle = "KPIs & tracking", Badge = "Coming soon", Disabled = true },
            });

            vm.TodayActions.AddRange(new[]
            {
                new TodayActionItem { Title = "Publish 1 high-leverage post", Subtitle = "Strategy Playbook • Make it obvious", Impact = "High", Minutes = 25 },
                new TodayActionItem { Title = "Review competitor pricing page", Subtitle = "33 Strategies • Intelligence", Impact = "Medium", Minutes = 15 }
            });

            vm.HabitSystems.AddRange(new[]
            {
                new HabitSystemItem
                {
                    Habit = "Daily KPI Review",
                    Streak = 7,
                    Status = "Stable",
                    Cue = "Open dashboard",
                    Environment = "Pinned tab + homepage",
                    Notes = "Keep the KPI widget visible."
                },
                new HabitSystemItem
                {
                    Habit = "Content Draft",
                    Streak = 3,
                    Status = "Building",
                    Cue = "End of workday",
                    Environment = "Saved template + checklist",
                    Notes = "Draft 1 idea before logging off."
                }
            });

            vm.StrategyCards.AddRange(new[]
            {
                new StrategyCardItem { Name = "Channel Domination", SourceBook = "33 Strategies", CorePrinciple = "Control the battlefield", ExecutionRate = 74, Effectiveness = "High" },
                new StrategyCardItem { Name = "Habit Flywheel", SourceBook = "Atomic Habits", CorePrinciple = "Reduce friction", ExecutionRate = 61, Effectiveness = "Medium" }
            });

            vm.Kpis.AddRange(new[]
            {
                new MetricKpiItem { Name = "Leads", Value = "42", Trend = "+12%", TrendDirection = "up", Subtext = "Last 7 days" },
                new MetricKpiItem { Name = "Conversion", Value = "3.1%", Trend = "-0.2%", TrendDirection = "down", Subtext = "Week over week" },
                new MetricKpiItem { Name = "CAC", Value = "$18", Trend = "Flat", TrendDirection = "flat", Subtext = "Rolling 30 days" }
            });

            return vm;
        }
    }

    public sealed class OverviewSummaryItem
    {
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public int Count { get; set; }
        public string? Badge { get; set; }
        public string Href { get; set; } = "#";
        public bool Disabled { get; set; }
    }

    public sealed class FeatureTileItem
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string? Badge { get; set; }
        public string Href { get; set; } = "#";
    }

    public sealed class TodayActionItem
    {
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string Principle { get; set; } = ""; // e.g., "Make it obvious"
        public string Impact { get; set; } = "Medium"; // Low/Medium/High
        public int Minutes { get; set; }
    }

    public sealed class HabitSystemItem
    {
        // Back-compat for the Razor view (Index.cshtml) which expects Name + StreakDays
        public string Name { get => Habit; set => Habit = value; }
        public int StreakDays { get => Streak; set => Streak = value; }

        // Canonical fields
        public string Habit { get; set; } = "";
        public int Streak { get; set; }
        public string Status { get; set; } = "";
        public string Cue { get; set; } = "";
        public string Environment { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    public sealed class StrategyCardItem
    {
        public string Name { get; set; } = "";
        public string SourceBook { get; set; } = "";
        public string CorePrinciple { get; set; } = "";
        public int ExecutionRate { get; set; } // 0-100
        public string Effectiveness { get; set; } = "Medium"; // Low/Medium/High
    }

    public sealed class MetricKpiItem
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
        public string Trend { get; set; } = "";
        public string TrendDirection { get; set; } = "flat"; // up/down/flat
        public string Subtext { get; set; } = "";
    }
}
