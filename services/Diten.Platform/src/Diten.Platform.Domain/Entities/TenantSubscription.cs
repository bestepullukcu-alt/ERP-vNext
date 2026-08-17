using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities;

public sealed class TenantSubscription : GlobalEntity
{
    public Guid TenantId { get; set; }
    public Guid PlanId { get; set; }
    public TenantSubscriptionStatus Status { get; set; } = TenantSubscriptionStatus.PendingProvisioning;
    public DateTimeOffset? TrialStartDateUtc { get; set; }
    public DateTimeOffset? TrialEndDateUtc { get; set; }
    public DateTimeOffset? CurrentPeriodStartUtc { get; set; }
    public DateTimeOffset? CurrentPeriodEndUtc { get; set; }
    public DateTimeOffset? ActivatedAtUtc { get; set; }
    public DateTimeOffset? RenewedAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public DateTimeOffset? ExpiredAtUtc { get; set; }
    public DateTimeOffset? SuspendedAtUtc { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public string? CancellationReason { get; set; }
    public string? Source { get; set; }
    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();
    public List<TenantSubscriptionHistoryEntry> History { get; set; } = [];
}

public sealed class TenantSubscriptionHistoryEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public TenantSubscriptionStatus Status { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? Actor { get; init; }
    public DateTimeOffset ChangedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CurrentPeriodStartUtc { get; init; }
    public DateTimeOffset? CurrentPeriodEndUtc { get; init; }
}
