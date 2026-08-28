using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0165 FU06 CyclePeriod master — one collection, tenant scoped, soft-delete aware. There is deliberately
/// <b>no delete method</b>: closing a period is the <c>closed</c> lifecycle, so a past plan stays explainable.
/// Every write is a single-document operation, so no multi-document transaction and no compensation is needed.
/// </summary>
public interface ICyclePeriodRepository
{
    Task<CyclePeriod?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>Every non-deleted period of the tenant (closed included — history must stay readable).</summary>
    Task<IReadOnlyList<CyclePeriod>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Every row carrying this code, so code uniqueness is decided in the handler rather than through a
    /// partial index with a <c>$ne</c> filter (which crash-loops the service at startup).</summary>
    Task<IReadOnlyList<CyclePeriod>> ListByCodeAsync(
        Guid tenantId, string cycleCode, CancellationToken cancellationToken);

    /// <summary>Every row of one planning year, for the sequence-uniqueness check and the year view. The
    /// business-unit scope is applied by the caller, because <c>null</c> is a scope of its own.</summary>
    Task<IReadOnlyList<CyclePeriod>> ListByYearAsync(Guid tenantId, int year, CancellationToken cancellationToken);

    /// <summary>Every ACTIVE period of the tenant — the overlap check and the resolver both work from this set, and
    /// both narrow it by scope themselves so the scope rule lives in exactly one place.</summary>
    Task<IReadOnlyList<CyclePeriod>> ListActiveAsync(Guid tenantId, CancellationToken cancellationToken);

    Task InsertAsync(CyclePeriod entity, CancellationToken cancellationToken);

    /// <summary>Optimistic replace: matches on (Id, TenantId, Version == expectedVersion) and bumps the token. Returns
    /// false on a concurrency mismatch so the handler can answer 409 instead of overwriting silently.</summary>
    Task<bool> ReplaceAsync(CyclePeriod entity, int expectedVersion, CancellationToken cancellationToken);
}
