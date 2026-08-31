using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Repositories;

public interface ITenantSubscriptionRepository
{
    Task<TenantSubscription> CreateAsync(IPlatformTransactionSession session, TenantSubscription subscription, CancellationToken ct = default) =>
        throw new PlatformTransactionUnavailableException("The subscription repository does not implement transaction-bound creates.");
    Task<TenantSubscription> CreateAsync(TenantSubscription subscription, CancellationToken ct = default);
    Task<TenantSubscription?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TenantSubscription?> GetByTenantIdAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct = default);
    Task<TenantSubscription?> GetCurrentByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<TenantSubscription>> GetHistoryByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> HasCurrentAsync(Guid tenantId, Guid? excludeSubscriptionId = null, CancellationToken ct = default);
    Task UpdateAsync(TenantSubscription subscription, byte[]? expectedRowVersion, CancellationToken ct = default);
    Task UpdateAsync(IPlatformTransactionSession session, TenantSubscription subscription, byte[]? expectedRowVersion, CancellationToken ct = default) =>
        throw new PlatformTransactionUnavailableException("The subscription repository does not implement transaction-bound updates.");
}

public static class TenantSubscriptionStatuses
{
    public static readonly TenantSubscriptionStatus[] Current =
    [
        TenantSubscriptionStatus.PendingProvisioning,
        TenantSubscriptionStatus.Trialing,
        TenantSubscriptionStatus.Active,
        TenantSubscriptionStatus.PastDue,
        TenantSubscriptionStatus.Suspended
    ];
}

public sealed class TenantSubscriptionConcurrencyException : Exception
{
    public TenantSubscriptionConcurrencyException()
        : base("Tenant subscription was modified by another process.")
    {
    }
}
