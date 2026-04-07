using System;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.Models;

public class NotificationItem
{
    public int Id { get; set; }

    [Required]
    public string OwnerId { get; set; } = string.Empty;

    [StringLength(32)]
    public string Type { get; set; } = "General";

    [StringLength(160)]
    public string Title { get; set; } = string.Empty;

    [StringLength(400)]
    public string Message { get; set; } = string.Empty;

    [StringLength(256)]
    public string? LinkUrl { get; set; }

    public bool IsRead { get; set; }
    public bool SendEmail { get; set; }
    public bool EmailSent { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
}
