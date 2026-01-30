using System;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.Models;

/// <summary>
/// Lightweight onboarding/profile record tied to an Identity user.
/// Kept separate from IdentityUser so onboarding can evolve independently.
/// </summary>
public class UserProfile
{
    public int Id { get; set; }

    [Required]
    [MaxLength(128)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Persona-style role selected during onboarding (e.g., Business Owner, Manager, Member, Viewer).
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string PersonaRole { get; set; } = "Business Owner";

    /// <summary>
    /// Optional onboarding note: what the user wants to accomplish.
    /// </summary>
    [MaxLength(500)]
    public string? PrimaryGoal { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
