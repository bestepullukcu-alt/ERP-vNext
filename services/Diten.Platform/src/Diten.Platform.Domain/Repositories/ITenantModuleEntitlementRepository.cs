using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Repositories;

public interface ITenantModuleEntitlementRepository
{
    Task<TenantModuleEntitlement> CreateAsync(IPlatformTransactionSession session, TenantModuleEntitlement entitlement, CancellationToken ct = default);
    [Obsolete("Authoritative entitlement mutations require an explicit Platform transaction session.")]
    Task<TenantModuleEntitlement> CreateAsync(TenantModuleEntitlement entitlement, CancellationToken ct = default);
    Task<TenantModuleEntitlement?> GetByIdAsync(Guid tenantId, Guid entitlementId, CancellationToken ct = default);
    Task<IReadOnlyList<TenantModuleEntitlement>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<long> CountEnabledAsync(IPlatformTransactionSession session, Guid tenantId, CancellationToken ct = default) =>
        throw new PlatformTransactionUnavailableException(
            "The entitlement repository does not implement transaction-bound enabled-count reads.");
    Task<IReadOnlyList<TenantModuleEntitlement>> GetByTenantAndModuleAsync(Guid tenantId, string moduleCode, CancellationToken ct = default);
    Task<TenantModuleEntitlement?> GetActiveBySourceAsync(Guid tenantId, string moduleCode, EntitlementSource source, Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateAsync(IPlatformTransactionSession session, TenantModuleEntitlement entitlement, byte[]? expectedRowVersion, CancellationToken ct = default);
    Task SoftDeleteAsync(IPlatformTransactionSession session, Guid tenantId, Guid entitlementId, byte[]? expectedRowVersion, CancellationToken ct = default);
    [Obsolete("Authoritative entitlement mutations require an explicit Platform transaction session.")]
    Task UpdateAsync(TenantModuleEntitlement entitlement, byte[]? expectedRowVersion, CancellationToken ct = default);
    [Obsolete("Authoritative entitlement mutations require an explicit Platform transaction session.")]
    Task SoftDeleteAsync(Guid tenantId, Guid entitlementId, byte[]? expectedRowVersion, CancellationToken ct = default);
}

public sealed class TenantModuleEntitlementConcurrencyException : Exception
{
    public TenantModuleEntitlementConcurrencyException()
        : base("Tenant module entitlement was modified by another process.")
    {
    }
}
