using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0155 FU01 PlannedVisit persistence — ONE collection (<c>planned_visits</c>), tenant scoped, soft-delete aware,
/// and with <b>no delete method</b>: a plan is cancelled/archived, because deleting one would take its history with it.
/// <para><see cref="PlannedVisit.PlannedDate"/> is a <see cref="DateOnly"/> stored as a "yyyy-MM-dd" string (see the
/// class map) so it is sortable/indexable without the DateTimeOffset parallel-arrays trap. ArchivedAt / CreatedAt /
/// UpdatedAt are DateTimeOffset (BSON arrays) and are never index keys and never server-side sort keys; ordering happens
/// in memory. Code uniqueness and the legacy overlap/same-day guards are enforced in the handlers, so no partial index
/// needs a <c>$ne</c> filter — which crash-loops the service at startup.</para>
/// <para>Every write is a single-document operation guarded by the optimistic <see cref="EntityBase.Version"/> token, so
/// no multi-document transaction and no compensation is needed on a standalone dev Mongo.</para>
/// </summary>
public sealed class PlannedVisitRepository : IPlannedVisitRepository
{
    public const string CollectionName = "planned_visits";

    private readonly IMongoCollection<PlannedVisit> _collection;

    public PlannedVisitRepository(IMongoDatabase database)
        => _collection = database.GetCollection<PlannedVisit>(CollectionName);

    private static FilterDefinition<PlannedVisit> Tenant(Guid tenantId)
        => Builders<PlannedVisit>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<PlannedVisit?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<PlannedVisit>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PlannedVisit>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return Ordered(rows);
    }

    public async Task<IReadOnlyList<PlannedVisit>> ListByCodeAsync(
        Guid tenantId, string visitCode, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<PlannedVisit>.Filter.Eq(x => x.VisitCode, visitCode))
            .ToListAsync(cancellationToken);
        return Ordered(rows);
    }

    public async Task<IReadOnlyList<PlannedVisit>> ListByResourceAndDateAsync(
        Guid tenantId, string resourceId, DateOnly plannedDate, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId)
                  & Builders<PlannedVisit>.Filter.Eq(x => x.Resource.ResourceId, resourceId)
                  & Builders<PlannedVisit>.Filter.Eq(x => x.PlannedDate, plannedDate))
            .ToListAsync(cancellationToken);
        return Ordered(rows);
    }

    /// <summary>WP-KP-CH-1 — tenant + PlannedDate ≥ fromDate (the "yyyy-MM-dd" string sorts like the date; served by
    /// ix_planned_visits_tenant_date_status) + any content item on one of the paths. No new index.</summary>
    public async Task<IReadOnlyList<PlannedVisit>> ListFromDateByContentPathsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> pathIds, DateOnly fromDate, CancellationToken cancellationToken)
    {
        if (pathIds.Count == 0)
        {
            return Array.Empty<PlannedVisit>();
        }

        var rows = await _collection
            .Find(Tenant(tenantId)
                  & Builders<PlannedVisit>.Filter.Gte(x => x.PlannedDate, fromDate)
                  & Builders<PlannedVisit>.Filter.ElemMatch(
                      x => x.ContentItems, Builders<PlannedVisitContentItem>.Filter.In(i => i.PathId, pathIds)))
            .ToListAsync(cancellationToken);
        return rows;
    }

    public async Task<IReadOnlyList<PlannedVisit>> ListByTargetAndDateAsync(
        Guid tenantId, Guid targetId, DateOnly plannedDate, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId)
                  & Builders<PlannedVisit>.Filter.Eq(x => x.TargetId, targetId)
                  & Builders<PlannedVisit>.Filter.Eq(x => x.PlannedDate, plannedDate))
            .ToListAsync(cancellationToken);
        return Ordered(rows);
    }

    /// <summary>W2-BE-d (6) — one rep's plans (tenant + Resource.ResourceId; the (tenant, resource, date) index prefix).</summary>
    public async Task<IReadOnlyList<PlannedVisit>> ListByResourceAsync(
        Guid tenantId, string resourceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return Array.Empty<PlannedVisit>();
        }

        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<PlannedVisit>.Filter.Eq(x => x.Resource.ResourceId, resourceId))
            .ToListAsync(cancellationToken);
        return Ordered(rows);
    }

    public async Task<IReadOnlyList<PlannedVisit>> ListByResourceAndContactsAsync(
        Guid tenantId, string resourceId, IReadOnlyCollection<Guid> contactIds, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || contactIds.Count == 0)
        {
            return Array.Empty<PlannedVisit>();
        }

        var ids = contactIds.Distinct().Select(id => (Guid?)id).ToList();
        var rows = await _collection
            .Find(Tenant(tenantId)
                  & Builders<PlannedVisit>.Filter.Eq(x => x.Resource.ResourceId, resourceId)
                  & Builders<PlannedVisit>.Filter.In(x => x.ContactId, ids))
            .ToListAsync(cancellationToken);
        return Ordered(rows);
    }

    public async Task<IReadOnlyList<PlannedVisit>> ListByIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<PlannedVisit>();
        }

        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<PlannedVisit>.Filter.In(x => x.Id, ids.Distinct()))
            .ToListAsync(cancellationToken);
        return Ordered(rows);
    }

    public async Task InsertAsync(PlannedVisit entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<bool> ReplaceAsync(PlannedVisit entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<PlannedVisit>.Filter.Where(
                x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.MatchedCount == 1;
    }

    /// <summary>In-memory ordering: newest planned day first, then VisitCode. Never a server-side sort over the date or
    /// the DateTimeOffset audit fields.</summary>
    private static IReadOnlyList<PlannedVisit> Ordered(IEnumerable<PlannedVisit> rows)
        => rows
            .OrderByDescending(x => x.PlannedDate)
            .ThenBy(x => x.VisitCode, StringComparer.Ordinal)
            .ToList();
}
