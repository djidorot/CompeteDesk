using System;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.Models;

/// <summary>
/// User-configurable "Key Metrics" definition for the Metrics & Momentum dashboard.
/// </summary>
public sealed class KeyMetricDefinition
{
    public int Id { get; set; }

    [Required]
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>
    /// Stable key used by the app (e.g. Revenue, Leads, ConversionRate, Engagement, OutputCount).
    /// </summary>
    [Required]
    [MaxLength(48)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Unit hint used for formatting (currency | number | percent).
    /// </summary>
    [Required]
    [MaxLength(24)]
    public string Unit { get; set; } = "number";

    public bool IsEnabled { get; set; } = true;

    public int SortOrder { get; set; } = 0;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
