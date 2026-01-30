using System;

namespace CompeteDesk.Models;

/// <summary>
/// Per-user controls for data retention, exports, and demo resets.
/// Keeps "log-like" data manageable (metrics traces, AI history) and supports onboarding flows.
/// </summary>
public sealed class UserDataControls
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Retention window (days) for log-like data. Intended for metrics/analytics sources such as:
    /// - DecisionTraces (AI History)
    /// - Analysis reports
    /// </summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    /// Preferred export format for downloads (csv/json).
    /// </summary>
    public string ExportFormat { get; set; } = "json";

    public DateTime? CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
