using System;
using System.Collections.Generic;

namespace CompeteDesk.ViewModels.Admin;

public sealed class AdminSubscriptionsViewModel
{
    public List<AdminSubscriptionUserRow> Users { get; set; } = new();
    public List<AdminPaymentRequestRow> PendingRequests { get; set; } = new();
}

public sealed class AdminSubscriptionUserRow
{
    public string UserId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Tier { get; set; } = "Free";
    public string Status { get; set; } = "Active";
    public int MonthlyAiLimit { get; set; }
    public int MonthlyExportLimit { get; set; }
    public int WorkspaceLimit { get; set; }
    public string? ExternalReference { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
}

public sealed class AdminPaymentRequestRow
{
    public int Id { get; set; }
    public string? UserEmail { get; set; }
    public string RequestedTier { get; set; } = "Pro";
    public string PaymentMethod { get; set; } = "QR";
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? Notes { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
}
