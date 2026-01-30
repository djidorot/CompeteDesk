using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.ViewModels.Onboarding;

public class OnboardingViewModel
{
    [Required]
    [Display(Name = "Your role")]
    public string PersonaRole { get; set; } = "Business Owner";

    [MaxLength(500)]
    [Display(Name = "Primary goal")]
    public string? PrimaryGoal { get; set; }

    public static IReadOnlyList<(string Key, string Title, string Description)> Roles { get; } = new List<(string, string, string)>
    {
        ("Business Owner", "Business Owner", "Set direction, approve strategy, monitor progress and competitors."),
        ("Manager", "Manager", "Turn strategy into execution plans and assign actions to the team."),
        ("Team Member", "Team Member", "Execute action items, update progress, and contribute intel."),
        ("Analyst", "Analyst", "Collect and structure competitive intel and analysis reports."),
        ("Advisor", "Advisor", "Review plans and suggest improvements (often read-only)."),
        ("Viewer", "Viewer", "View dashboards and reports with read-only access."),
    };
}
