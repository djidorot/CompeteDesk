using System;

namespace CompeteDesk.Models.Common;

/// <summary>
/// Common audit fields for entities that are persisted in the app database.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; set; }
    DateTime? UpdatedAtUtc { get; set; }

    /// <summary>
    /// IdentityUser.Id of the user who created the record.
    /// </summary>
    string? CreatedById { get; set; }

    /// <summary>
    /// IdentityUser.Id of the user who last updated the record.
    /// </summary>
    string? UpdatedById { get; set; }
}

/// <summary>
/// Soft delete contract to avoid orphaning related rows and to keep history.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    string? DeletedById { get; set; }
}
