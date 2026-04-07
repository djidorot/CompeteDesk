using CompeteDesk.Data;
using CompeteDesk.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace CompeteDesk.Services.Notifications;

public sealed class InAppNotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _emailSender;

    public InAppNotificationService(ApplicationDbContext db, IEmailSender emailSender)
    {
        _db = db;
        _emailSender = emailSender;
    }

    public async Task CreateAsync(string ownerId, string title, string message, string type = "General", string? linkUrl = null, string? email = null, bool sendEmail = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            return;

        var item = new NotificationItem
        {
            OwnerId = ownerId,
            Title = title.Trim(),
            Message = message.Trim(),
            Type = string.IsNullOrWhiteSpace(type) ? "General" : type.Trim(),
            LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim(),
            SendEmail = sendEmail,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Notifications.Add(item);
        await _db.SaveChangesAsync(ct);

        if (sendEmail && !string.IsNullOrWhiteSpace(email))
        {
            await _emailSender.SendEmailAsync(email, title, $"<p>{System.Net.WebUtility.HtmlEncode(message)}</p>");
            item.EmailSent = true;
            await _db.SaveChangesAsync(ct);
        }
    }

    public Task<int> GetUnreadCountAsync(string ownerId, CancellationToken ct = default)
        => _db.Notifications.AsNoTracking().CountAsync(x => x.OwnerId == ownerId && !x.IsRead, ct);
}
