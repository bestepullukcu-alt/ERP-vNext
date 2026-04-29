using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface ITenantDomainRepository
{
    Task<TenantDomain> CreateAsync(TenantDomain domain, CancellationToken ct = default);
    Task<TenantDomain?> GetByDomainNameAsync(string domainName, CancellationToken ct = default);
    Task<IReadOnlyList<TenantDomain>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantDomain?> GetPrimaryByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task UpdateAsync(TenantDomain domain, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
