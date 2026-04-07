using Microsoft.AspNetCore.Identity.UI.Services;

namespace CompeteDesk.Services.Notifications;

/// <summary>
/// Safe no-op email sender used when SendGrid is not configured.
/// This keeps local/dev and minimal deployments functional without wiring dead integrations.
/// </summary>
public sealed class NullEmailSender : IEmailSender
{
    private readonly ILogger<NullEmailSender> _logger;

    public NullEmailSender(ILogger<NullEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        _logger.LogInformation(
            "Email skipped because SendGrid is not configured. To={To} Subject={Subject}",
            email,
            subject);

        return Task.CompletedTask;
    }
}
