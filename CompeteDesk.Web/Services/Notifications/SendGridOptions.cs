namespace CompeteDesk.Services.Notifications;

public sealed class SendGridOptions
{
    public string? ApiKey { get; set; }

    /// <summary>
    /// Verified sender email configured in SendGrid.
    /// </summary>
    public string? FromEmail { get; set; }

    /// <summary>
    /// Friendly from name.
    /// </summary>
    public string? FromName { get; set; }
}
