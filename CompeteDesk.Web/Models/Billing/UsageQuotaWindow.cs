using System;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.Models.Billing;

public sealed class UsageQuotaWindow
{
    public int Id { get; set; }

    [Required]
    [StringLength(128)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [StringLength(16)]
    public string PeriodKey { get; set; } = string.Empty;

    public int AiRequestsUsed { get; set; }
    public int ExportsUsed { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
