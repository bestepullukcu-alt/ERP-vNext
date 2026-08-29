using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0167 FU02 Segment master (criteria embedded, D2 — one collection, one optimistic token). Tenant scoped and
/// soft-delete aware. There is deliberately <b>no delete method</b>: closing a segment is the soft archive lifecycle,
/// so a past selection stays explainable. Every write is a single-document operation, so no multi-document transaction
/// and no compensation is needed.
/// </summary>
public interface ISegmentRepository
{
    Task<Segment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>Every non-deleted segment of the tenant (archived included — history must stay readable).</summary>
    Task<IReadOnlyList<Segment>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>All versions sharing a lineage, so a new-version clone can compute the next SegmentVersion and the
    /// activate path can supersede its predecessor.</summary>
    Task<IReadOnlyList<Segment>> ListByLineageAsync(
        Guid tenantId, Guid versionLineageId, CancellationToken cancellationToken);

    /// <summary>Every row carrying this code (any version, archived included), so code uniqueness is decided in the
    /// handler rather than through a partial index with a <c>$ne</c> filter (which crash-loops the service).</summary>
    Task<IReadOnlyList<Segment>> ListByCodeAsync(Guid tenantId, string segmentCode, CancellationToken cancellationToken);

    Task InsertAsync(Segment entity, CancellationToken cancellationToken);

    /// <summary>Optimistic replace: matches on (Id, TenantId, Version == expectedVersion) and bumps the token. Returns
    /// false on a concurrency mismatch so the handler can answer 409 instead of overwriting silently.</summary>
    Task<bool> ReplaceAsync(Segment entity, int expectedVersion, CancellationToken cancellationToken);
}
