using System;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.Models.Billing;

public sealed class UserSubscription
{
    public int Id { get; set; }

    [Required]
    [StringLength(128)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [StringLength(24)]
    public string Tier { get; set; } = "Free";

    [Required]
    [StringLength(24)]
    public string Status { get; set; } = "Active";

    [StringLength(32)]
    public string BillingProvider { get; set; } = "Manual";

    [StringLength(128)]
    public string? ExternalReference { get; set; }

    public int MonthlyAiLimit { get; set; }
    public int MonthlyExportLimit { get; set; }
    public int WorkspaceLimit { get; set; }

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndsAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
}
