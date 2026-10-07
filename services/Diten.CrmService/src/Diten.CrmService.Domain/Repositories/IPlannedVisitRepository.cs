using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0155 FU01 PlannedVisit master — one collection, tenant scoped, soft-delete aware. There is deliberately
/// <b>no delete method</b> (§8.2): a plan is cancelled and/or archived, and history stays readable. Every write is a
/// single-document operation guarded by the optimistic <see cref="EntityBase.Version"/> token, so no multi-document
/// transaction and no compensation is needed (§8.4).
/// </summary>
public interface IPlannedVisitRepository
{
    Task<PlannedVisit?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>Every non-deleted plan of the tenant (archived included — history must stay readable). Filtering and
    /// ordering happen in memory to avoid sorting the DateOnly/DateTimeOffset fields at the server (parallel-arrays).</summary>
    Task<IReadOnlyList<PlannedVisit>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Every row carrying this VisitCode, so code uniqueness is decided in the handler rather than through a
    /// partial index with a <c>$ne</c> filter (which crash-loops the service at startup).</summary>
    Task<IReadOnlyList<PlannedVisit>> ListByCodeAsync(Guid tenantId, string visitCode, CancellationToken cancellationToken);

    /// <summary>Every plan of one resource on one day — the overlap guard's (V22) pre-check.</summary>
    Task<IReadOnlyList<PlannedVisit>> ListByResourceAndDateAsync(
        Guid tenantId, string resourceId, DateOnly plannedDate, CancellationToken cancellationToken);

    /// <summary>Every plan against one target on one day — the same-day-same-type guard's (V24) pre-check.</summary>
    Task<IReadOnlyList<PlannedVisit>> ListByTargetAndDateAsync(
        Guid tenantId, Guid targetId, DateOnly plannedDate, CancellationToken cancellationToken);

    /// <summary>WP-KP-CH-1 — the plans on or after <paramref name="fromDate"/> whose content items tell one of
    /// <paramref name="pathIds"/> (SB-3b <c>ContentItems[].PathId</c>). A NARROWING read only: whether a plan counts
    /// (not cancelled / archived) is decided by the caller's rule, so the rule is one place and testable.</summary>
    Task<IReadOnlyList<PlannedVisit>> ListFromDateByContentPathsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> pathIds, DateOnly fromDate, CancellationToken cancellationToken);

    /// <summary>WP-VP-3D — one rep's plans against a set of doctors (any date, archived / cancelled included: whether a
    /// plan counts is the caller's rule). ONE read for the whole set — the period-status reads never ask per doctor. The
    /// default narrows <see cref="ListAsync"/> in memory; the Mongo repository answers with a single
    /// <c>Resource.ResourceId = r AND ContactId IN (...)</c> find on the existing (tenant, resource, date) index prefix.</summary>
    async Task<IReadOnlyList<PlannedVisit>> ListByResourceAndContactsAsync(
        Guid tenantId, string resourceId, IReadOnlyCollection<Guid> contactIds, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || contactIds is null || contactIds.Count == 0)
        {
            return Array.Empty<PlannedVisit>();
        }

        var wanted = contactIds.ToHashSet();
        return (await ListAsync(tenantId, cancellationToken))
            .Where(p => p.ContactId is { } c && wanted.Contains(c)
                        && string.Equals(p.Resource?.ResourceId, resourceId, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>WP-VP-4A — the non-deleted plans with these ids in ONE read (an old committed plan's written visits). The
    /// default narrows <see cref="ListAsync"/>; the Mongo repository answers with a single <c>_id $in</c> find.</summary>
    async Task<IReadOnlyList<PlannedVisit>> ListByIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids is null || ids.Count == 0)
        {
            return Array.Empty<PlannedVisit>();
        }

        var wanted = ids.ToHashSet();
        return (await ListAsync(tenantId, cancellationToken)).Where(p => wanted.Contains(p.Id)).ToList();
    }

    Task InsertAsync(PlannedVisit entity, CancellationToken cancellationToken);

    /// <summary>Optimistic replace: matches on (Id, TenantId, Version == expectedVersion) and bumps the token. Returns
    /// false on a concurrency mismatch so the handler can answer 409 instead of overwriting silently.</summary>
    Task<bool> ReplaceAsync(PlannedVisit entity, int expectedVersion, CancellationToken cancellationToken);
}
