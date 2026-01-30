using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace CompeteDesk.Services.Notifications;

/// <summary>
/// Sends Identity verification emails via SendGrid.
/// 
/// If SendGrid is not configured (missing API key / sender), the email will be
/// logged so local development doesn't crash.
/// </summary>
public sealed class SendGridEmailSender : IEmailSender
{
    private readonly ILogger<SendGridEmailSender> _logger;
    private readonly SendGridOptions _options;

    public SendGridEmailSender(IOptions<SendGridOptions> options, ILogger<SendGridEmailSender> logger)
    {
        _logger = logger;
        _options = options.Value ?? new SendGridOptions();
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey)
            || string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            _logger.LogWarning(
                "SendGrid is not configured (SendGrid:ApiKey and/or SendGrid:FromEmail missing). " +
                "Skipping actual send. To={To} Subject={Subject}. Body (first 200 chars)={Body}",
                email,
                subject,
                htmlMessage?.Length > 200 ? htmlMessage[..200] : htmlMessage);

            return;
        }

        var client = new SendGridClient(_options.ApiKey);
        var from = new EmailAddress(_options.FromEmail, string.IsNullOrWhiteSpace(_options.FromName) ? "CompeteDesk" : _options.FromName);
        var to = new EmailAddress(email);

        // Identity sends HTML already (verification link).
        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent: null, htmlContent: htmlMessage);
        var response = await client.SendEmailAsync(msg);

        if ((int)response.StatusCode >= 400)
        {
            var body = await response.Body.ReadAsStringAsync();
            _logger.LogError("SendGrid send failed. Status={Status} Body={Body}", response.StatusCode, body);
        }
    }
}
