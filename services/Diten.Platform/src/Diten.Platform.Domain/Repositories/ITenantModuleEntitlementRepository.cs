using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Repositories;

public interface ITenantModuleEntitlementRepository
{
    Task<TenantModuleEntitlement> CreateAsync(TenantModuleEntitlement entitlement, CancellationToken ct = default);
    Task<TenantModuleEntitlement?> GetByIdAsync(Guid tenantId, Guid entitlementId, CancellationToken ct = default);
    Task<IReadOnlyList<TenantModuleEntitlement>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<TenantModuleEntitlement>> GetByTenantAndModuleAsync(Guid tenantId, string moduleCode, CancellationToken ct = default);
    Task<TenantModuleEntitlement?> GetActiveBySourceAsync(Guid tenantId, string moduleCode, EntitlementSource source, Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateAsync(TenantModuleEntitlement entitlement, byte[]? expectedRowVersion, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid tenantId, Guid entitlementId, byte[]? expectedRowVersion, CancellationToken ct = default);
}

public sealed class TenantModuleEntitlementConcurrencyException : Exception
{
    public TenantModuleEntitlementConcurrencyException()
        : base("Tenant module entitlement was modified by another process.")
    {
    }
}
