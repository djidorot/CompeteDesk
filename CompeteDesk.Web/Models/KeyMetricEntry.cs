using System;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.Models;

/// <summary>
/// A single point in time for a KeyMetricDefinition.
/// Stored per-day (DateUtc) but can be used for any range bucketing.
/// </summary>
public sealed class KeyMetricEntry
{
    public int Id { get; set; }

    [Required]
    public int DefinitionId { get; set; }

    [Required]
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>
    /// Date in UTC (stored as DateTime at midnight UTC).
    /// </summary>
    public DateTime DateUtc { get; set; }

    public decimal Value { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    // Optional navigation
    public KeyMetricDefinition? Definition { get; set; }
}
