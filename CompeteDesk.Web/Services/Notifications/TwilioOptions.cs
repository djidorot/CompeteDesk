namespace CompeteDesk.Services.Notifications;

public sealed class TwilioOptions
{
    public string? AccountSid { get; set; }
    public string? AuthToken { get; set; }

    /// <summary>
    /// Your Twilio phone number in E.164 format, e.g. "+15551234567".
    /// </summary>
    public string? FromPhoneNumber { get; set; }
}
