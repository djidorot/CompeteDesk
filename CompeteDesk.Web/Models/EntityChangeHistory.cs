using System;

namespace CompeteDesk.Models;

/// <summary>
/// Version/change history per entity ("topic"). Stores before/after JSON snapshots.
/// Keep payloads small; use this for accountability and rollback/support.
/// </summary>
public class EntityChangeHistory
{
    public int Id { get; set; }

    public string? OwnerId { get; set; }
    public string? ActorUserId { get; set; }
    public string? ActorEmail { get; set; }

    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Action { get; set; } = ""; // Created|Updated|Deleted

    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
