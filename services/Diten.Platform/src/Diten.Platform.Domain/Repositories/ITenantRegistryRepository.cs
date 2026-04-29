using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface ITenantRegistryRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default);
    Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default);
    Task<Tenant> CreateAsync(Tenant tenant, CancellationToken ct = default);
    Task UpdateAsync(Tenant tenant, CancellationToken ct = default);
    Task UpdateStatusAsync(Guid id, TenantStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(TenantListQuery query, CancellationToken ct = default);
    Task<TenantRegistryStats> GetStatsAsync(CancellationToken ct = default);
}

public sealed record TenantListQuery(
    string? Search,
    string? Status,
    string? Region,
    int Page,
    int PageSize,
    string Sort);

public sealed record TenantRegistryStats(
    long Total,
    long Active,
    long Provisioning,
    long Suspended,
    long Deactivated);
