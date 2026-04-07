using System;
using System.Threading;
using System.Threading.Tasks;
using CompeteDesk.Data;
using CompeteDesk.Models.Billing;
using Microsoft.EntityFrameworkCore;

namespace CompeteDesk.Services.Billing;

public sealed class SubscriptionService
{
    private readonly ApplicationDbContext _db;

    public SubscriptionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public sealed class TierDefinition
    {
        public string Tier { get; init; } = "Free";
        public int MonthlyAiLimit { get; init; }
        public int MonthlyExportLimit { get; init; }
        public int WorkspaceLimit { get; init; }
        public string BadgeClass { get; init; } = "secondary";
    }

    public sealed class UsageSnapshot
    {
        public TierDefinition Plan { get; init; } = GetTierDefinition("Free");
        public int AiUsed { get; init; }
        public int ExportsUsed { get; init; }
        public int WorkspacesUsed { get; init; }
        public int AiRemaining => Math.Max(0, Plan.MonthlyAiLimit - AiUsed);
        public int ExportsRemaining => Math.Max(0, Plan.MonthlyExportLimit - ExportsUsed);
        public int WorkspacesRemaining => Math.Max(0, Plan.WorkspaceLimit - WorkspacesUsed);
        public string PeriodKey { get; init; } = CurrentPeriodKey();
        public string? Status { get; init; }
    }

    public static string CurrentPeriodKey(DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        return $"{now:yyyy-MM}";
    }

    public static TierDefinition GetTierDefinition(string? tier)
        => (tier ?? "Free").Trim().ToLowerInvariant() switch
        {
            "premium" => new TierDefinition { Tier = "Premium", MonthlyAiLimit = 400, MonthlyExportLimit = 150, WorkspaceLimit = 25, BadgeClass = "warning" },
            "pro" => new TierDefinition { Tier = "Pro", MonthlyAiLimit = 120, MonthlyExportLimit = 40, WorkspaceLimit = 8, BadgeClass = "primary" },
            _ => new TierDefinition { Tier = "Free", MonthlyAiLimit = 20, MonthlyExportLimit = 8, WorkspaceLimit = 2, BadgeClass = "secondary" }
        };

    public async Task<UserSubscription> GetOrCreateSubscriptionAsync(string userId, CancellationToken ct = default)
    {
        var existing = await _db.UserSubscriptions.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (existing is not null)
        {
            ApplyTierDefaults(existing);
            return existing;
        }

        var created = CreateDefaultSubscription(userId);
        _db.UserSubscriptions.Add(created);
        await _db.SaveChangesAsync(ct);
        return created;
    }

    public async Task<UsageSnapshot> GetUsageSnapshotAsync(string userId, CancellationToken ct = default)
    {
        var subscription = await GetOrCreateSubscriptionAsync(userId, ct);
        var plan = GetTierDefinition(subscription.Tier);
        var periodKey = CurrentPeriodKey();
        var window = await _db.UsageQuotaWindows.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.PeriodKey == periodKey, ct);
        var workspacesUsed = await _db.Workspaces.AsNoTracking().CountAsync(x => x.OwnerId == userId, ct);

        return new UsageSnapshot
        {
            Plan = plan,
            AiUsed = window?.AiRequestsUsed ?? 0,
            ExportsUsed = window?.ExportsUsed ?? 0,
            WorkspacesUsed = workspacesUsed,
            PeriodKey = periodKey,
            Status = subscription.Status
        };
    }

    public async Task<(bool Allowed, string? Error)> CanCreateWorkspaceAsync(string userId, CancellationToken ct = default)
    {
        var usage = await GetUsageSnapshotAsync(userId, ct);
        if (usage.WorkspacesUsed >= usage.Plan.WorkspaceLimit)
        {
            return (false, $"Your {usage.Plan.Tier} plan allows up to {usage.Plan.WorkspaceLimit} workspace{(usage.Plan.WorkspaceLimit == 1 ? string.Empty : "s")}. Upgrade to create more.");
        }

        return (true, null);
    }

    public async Task<(bool Allowed, string? Error)> CanUseAiAsync(string userId, CancellationToken ct = default)
    {
        var usage = await GetUsageSnapshotAsync(userId, ct);
        if (usage.AiUsed >= usage.Plan.MonthlyAiLimit)
        {
            return (false, $"You reached your monthly AI quota ({usage.Plan.MonthlyAiLimit}) for the {usage.Plan.Tier} plan.");
        }

        return (true, null);
    }

    public async Task<(bool Allowed, string? Error)> CanExportAsync(string userId, CancellationToken ct = default)
    {
        var usage = await GetUsageSnapshotAsync(userId, ct);
        if (usage.ExportsUsed >= usage.Plan.MonthlyExportLimit)
        {
            return (false, $"You reached your monthly export quota ({usage.Plan.MonthlyExportLimit}) for the {usage.Plan.Tier} plan.");
        }

        return (true, null);
    }

    public async Task RecordAiUsageAsync(string userId, CancellationToken ct = default)
    {
        var window = await GetOrCreateWindowAsync(userId, ct);
        window.AiRequestsUsed += 1;
        window.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RecordExportUsageAsync(string userId, CancellationToken ct = default)
    {
        var window = await GetOrCreateWindowAsync(userId, ct);
        window.ExportsUsed += 1;
        window.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ApprovePaymentRequestAsync(int requestId, string reviewerUserId, CancellationToken ct = default)
    {
        var request = await _db.SubscriptionPaymentRequests.FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null) return;

        request.Status = "Approved";
        request.ReviewedAtUtc = DateTime.UtcNow;
        request.ReviewedByUserId = reviewerUserId;

        var subscription = await GetOrCreateSubscriptionAsync(request.UserId, ct);
        subscription.Tier = NormalizeTier(request.RequestedTier);
        subscription.Status = "Active";
        subscription.BillingProvider = NormalizePaymentMethod(request.PaymentMethod);
        subscription.ExternalReference = request.ReferenceNumber;
        subscription.ApprovedByUserId = reviewerUserId;
        subscription.ApprovedAtUtc = DateTime.UtcNow;
        subscription.UpdatedAtUtc = DateTime.UtcNow;
        subscription.StartedAtUtc = subscription.StartedAtUtc == default ? DateTime.UtcNow : subscription.StartedAtUtc;
        ApplyTierDefaults(subscription);

        await _db.SaveChangesAsync(ct);
    }

    public async Task RejectPaymentRequestAsync(int requestId, string reviewerUserId, string? notes, CancellationToken ct = default)
    {
        var request = await _db.SubscriptionPaymentRequests.FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null) return;

        request.Status = "Rejected";
        request.Notes = string.IsNullOrWhiteSpace(notes) ? request.Notes : notes.Trim();
        request.ReviewedAtUtc = DateTime.UtcNow;
        request.ReviewedByUserId = reviewerUserId;
        await _db.SaveChangesAsync(ct);
    }

    public UserSubscription CreateDefaultSubscription(string userId)
    {
        var sub = new UserSubscription
        {
            UserId = userId,
            Tier = "Free",
            Status = "Active",
            BillingProvider = "Manual",
            StartedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };
        ApplyTierDefaults(sub);
        return sub;
    }

    public void ApplyTierDefaults(UserSubscription subscription)
    {
        var plan = GetTierDefinition(subscription.Tier);
        subscription.Tier = plan.Tier;
        subscription.MonthlyAiLimit = plan.MonthlyAiLimit;
        subscription.MonthlyExportLimit = plan.MonthlyExportLimit;
        subscription.WorkspaceLimit = plan.WorkspaceLimit;
    }

    public static string NormalizeTier(string? tier)
        => GetTierDefinition(tier).Tier;

    public static string NormalizePaymentMethod(string? paymentMethod)
    {
        var value = (paymentMethod ?? "QR").Trim();
        return string.Equals(value, "Stripe", StringComparison.OrdinalIgnoreCase) ? "Stripe" : "QR";
    }

    private async Task<UsageQuotaWindow> GetOrCreateWindowAsync(string userId, CancellationToken ct)
    {
        var periodKey = CurrentPeriodKey();
        var existing = await _db.UsageQuotaWindows.FirstOrDefaultAsync(x => x.UserId == userId && x.PeriodKey == periodKey, ct);
        if (existing is not null) return existing;

        var created = new UsageQuotaWindow
        {
            UserId = userId,
            PeriodKey = periodKey,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.UsageQuotaWindows.Add(created);
        await _db.SaveChangesAsync(ct);
        return created;
    }
}
