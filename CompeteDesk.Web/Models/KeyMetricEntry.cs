using System;
using System.ComponentModel.DataAnnotations;
using CompeteDesk.Models.Common;

namespace CompeteDesk.Models;

/// <summary>
/// A single point in time for a KeyMetricDefinition.
/// Stored per-day (DateUtc) but can be used for any range bucketing.
/// </summary>
public sealed class KeyMetricEntry : IAuditableEntity, ISoftDeletable
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

    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedById { get; set; }

    // Optional navigation
    public KeyMetricDefinition? Definition { get; set; }
}
