using System;

namespace CompeteDesk.Models;

/// <summary>
/// Lightweight audit/event log for security + accountability.
/// Captures high-level actions (create/update/delete) and request metadata.
/// </summary>
public class AuditLog
{
    public int Id { get; set; }

    /// <summary>Tenant/owner for filtering (usually the same as ActorUserId for single-user data).</summary>
    public string? OwnerId { get; set; }

    public string? ActorUserId { get; set; }
    public string? ActorEmail { get; set; }

    public string Action { get; set; } = ""; // Created|Updated|Deleted|Viewed|Exported...
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }

    public string? Summary { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
