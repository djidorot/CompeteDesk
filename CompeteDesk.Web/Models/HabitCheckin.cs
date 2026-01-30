using System;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.Models;

/// <summary>
/// A completion log for a habit. Stored as one row per day.
/// </summary>
public class HabitCheckin
{
    public int Id { get; set; }

    [Required]
    public int HabitId { get; set; }

    [Required]
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>
    /// Date in UTC (date-only semantics).
    /// </summary>
    public DateTime OccurredOnUtc { get; set; }

    /// <summary>
    /// Number of times completed on that day (useful for weekly habits).
    /// </summary>
    public int Count { get; set; } = 1;

    [MaxLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
