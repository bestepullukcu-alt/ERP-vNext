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

/// <summary>
/// BL-500 — a tenant-scoped caller asked to write a row of another tenant. Refused before any write. Its own type so the
/// refusal is never mistaken for a concurrency conflict (it used to reuse <see cref="TenantModuleEntitlementConcurrencyException"/>
/// and reached the screen as "somebody else changed this row"); the handlers answer it exactly as "not found", so the
/// answer leaks nothing about another tenant's rows.
/// </summary>
public sealed class TenantModuleEntitlementTenantMismatchException : Exception
{
    public TenantModuleEntitlementTenantMismatchException()
        : base("The tenant module entitlement belongs to another tenant than the caller's.")
    {
    }
}
