using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface ITenantRegistryRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>INTX FIX2 — the tenant as this transaction sees it (never a copy read before the transaction began).</summary>
    Task<Tenant?> GetByIdAsync(IPlatformTransactionSession session, Guid id, CancellationToken ct = default) =>
        throw new PlatformTransactionUnavailableException("The tenant repository does not implement transaction-bound reads.");
    Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default);
    Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default);
    Task<Tenant> CreateAsync(Tenant tenant, CancellationToken ct = default);
    Task UpdateAsync(Tenant tenant, CancellationToken ct = default);
    Task UpdateAsync(IPlatformTransactionSession session, Tenant tenant, CancellationToken ct = default) =>
        throw new PlatformTransactionUnavailableException("The tenant repository does not implement transaction-bound updates.");
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task UpdateStatusAsync(Guid id, TenantStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(TenantListQuery query, CancellationToken ct = default);
    Task<TenantRegistryStats> GetStatsAsync(CancellationToken ct = default);
    /// <summary>BL-521 — the same counts over <paramref name="onlyTenantIds"/> only (null = every tenant). A repository
    /// that cannot filter refuses rather than count everyone.</summary>
    Task<TenantRegistryStats> GetStatsAsync(IReadOnlyCollection<Guid>? onlyTenantIds, CancellationToken ct = default) =>
        onlyTenantIds is null ? GetStatsAsync(ct) : throw new NotSupportedException("This tenant repository cannot count a tenant subset.");
}

public sealed record TenantListQuery(
    string? Search,
    string? Status,
    string? Region,
    int Page,
    int PageSize,
    string Sort,
    // BL-521 — null = every tenant; otherwise only these (possibly none: a partner administrator's scope).
    IReadOnlyCollection<Guid>? OnlyTenantIds = null);

public sealed record TenantRegistryStats(
    long Total,
    long Active,
    long Provisioning,
    long Suspended,
    long Deactivated,
    long Trial,
    long OverQuota);
