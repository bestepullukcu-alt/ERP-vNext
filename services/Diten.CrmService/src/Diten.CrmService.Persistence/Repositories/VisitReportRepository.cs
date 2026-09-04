using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0155 FU02 VisitReport persistence — ONE collection (<c>visit_reports</c>), tenant scoped, soft-delete aware, and
/// with <b>no delete method</b>: a report is an immutable compliance record, corrections are append-only amendments.
/// <para><see cref="VisitReport.ExecutedAt"/> / SubmittedAt / AmendedAt / CreatedAt / UpdatedAt are DateTimeOffset (BSON
/// arrays) and are NEVER index keys and NEVER server-side sort keys — ordering happens in memory (the CRM parallel-arrays
/// trap). The 1:1-per-plan guard is enforced in the handler (an existing report by PlannedVisitId), so no partial index
/// needs a <c>$ne</c> filter — which crash-loops the service at startup.</para>
/// <para>Every write is a single-document operation guarded by the optimistic <see cref="EntityBase.Version"/> token, so
/// no multi-document transaction and no compensation is needed on a standalone dev Mongo.</para>
/// </summary>
public sealed class VisitReportRepository : IVisitReportRepository
{
    public const string CollectionName = "visit_reports";

    private readonly IMongoCollection<VisitReport> _collection;

    public VisitReportRepository(IMongoDatabase database)
        => _collection = database.GetCollection<VisitReport>(CollectionName);

    private static FilterDefinition<VisitReport> Tenant(Guid tenantId)
        => Builders<VisitReport>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<VisitReport?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<VisitReport>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<VisitReport?> GetByPlannedVisitIdAsync(
        Guid tenantId, Guid plannedVisitId, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<VisitReport>.Filter.Eq(x => x.PlannedVisitId, plannedVisitId))
            .ToListAsync(cancellationToken);
        // 1:1 by construction; if a race ever produced two, the earliest-created is the authoritative one.
        return rows.OrderBy(x => x.CreatedAt).FirstOrDefault();
    }

    public async Task<IReadOnlyList<VisitReport>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return Ordered(rows);
    }

    public async Task<IReadOnlyList<VisitReport>> ListByPlannedVisitIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> plannedVisitIds, CancellationToken cancellationToken)
    {
        if (plannedVisitIds is null || plannedVisitIds.Count == 0)
        {
            return Array.Empty<VisitReport>();
        }

        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<VisitReport>.Filter.In(x => x.PlannedVisitId, plannedVisitIds))
            .ToListAsync(cancellationToken);
        return Ordered(rows);
    }

    public async Task InsertAsync(VisitReport entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<bool> ReplaceAsync(VisitReport entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<VisitReport>.Filter.Where(
                x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.MatchedCount == 1;
    }

    /// <summary>In-memory ordering: newest execution first. Never a server-side sort over the DateTimeOffset fields.</summary>
    private static IReadOnlyList<VisitReport> Ordered(IEnumerable<VisitReport> rows)
        => rows.OrderByDescending(x => x.ExecutedAt).ToList();
}
