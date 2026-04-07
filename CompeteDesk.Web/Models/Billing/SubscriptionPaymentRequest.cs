using System;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.Models.Billing;

public sealed class SubscriptionPaymentRequest
{
    public int Id { get; set; }

    [Required]
    [StringLength(128)]
    public string UserId { get; set; } = string.Empty;

    [StringLength(256)]
    public string? UserEmail { get; set; }

    [Required]
    [StringLength(24)]
    public string RequestedTier { get; set; } = "Pro";

    [Required]
    [StringLength(24)]
    public string PaymentMethod { get; set; } = "QR";

    [Required]
    [StringLength(80)]
    public string ReferenceNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(24)]
    public string Status { get; set; } = "Pending";

    [StringLength(400)]
    public string? Notes { get; set; }

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }

    [StringLength(128)]
    public string? ReviewedByUserId { get; set; }
}
