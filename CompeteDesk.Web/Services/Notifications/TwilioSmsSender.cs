using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace CompeteDesk.Services.Notifications;

/// <summary>
/// Lightweight SMS sender via Twilio.
/// 
/// Not directly used by default Identity UI. It's provided so you can add an
/// optional phone verification flow later (or 2FA codes) without changing the
/// core notification plumbing.
/// </summary>
public sealed class TwilioSmsSender
{
    private readonly ILogger<TwilioSmsSender> _logger;
    private readonly TwilioOptions _options;

    public TwilioSmsSender(IOptions<TwilioOptions> options, ILogger<TwilioSmsSender> logger)
    {
        _logger = logger;
        _options = options.Value ?? new TwilioOptions();
    }

    public async Task<bool> SendSmsAsync(string toPhoneNumberE164, string message)
    {
        if (string.IsNullOrWhiteSpace(_options.AccountSid)
            || string.IsNullOrWhiteSpace(_options.AuthToken)
            || string.IsNullOrWhiteSpace(_options.FromPhoneNumber))
        {
            _logger.LogWarning(
                "Twilio is not configured (Twilio:AccountSid/AuthToken/FromPhoneNumber missing). " +
                "Skipping actual SMS send. To={To}. Message (first 200 chars)={Message}",
                toPhoneNumberE164,
                message?.Length > 200 ? message[..200] : message);

            return false;
        }

        TwilioClient.Init(_options.AccountSid, _options.AuthToken);

        try
        {
            var msg = await MessageResource.CreateAsync(
                to: new Twilio.Types.PhoneNumber(toPhoneNumberE164),
                from: new Twilio.Types.PhoneNumber(_options.FromPhoneNumber),
                body: message);

            _logger.LogInformation("Twilio SMS sent. Sid={Sid} To={To}", msg.Sid, toPhoneNumberE164);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twilio SMS send failed. To={To}", toPhoneNumberE164);
            return false;
        }
    }
}
