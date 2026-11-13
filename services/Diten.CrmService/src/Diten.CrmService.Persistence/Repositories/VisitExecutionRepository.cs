using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// WP-VW-W3 (BE-a) — <c>visit_executions</c>: one record per planned visit, tenant scoped. The 1:1 rule is the unique
/// index <c>ux_visit_executions_tenant_planned_visit</c> (equality-only partial filter): a concurrent second start gets
/// a duplicate-key error, answered as <c>false</c>. StartedAt / FinishedAt and the evidence instants are DateTimeOffset
/// (BSON arrays) — never index keys, never server-side sort keys (the CRM parallel-arrays 500). No delete.
/// </summary>
public sealed class VisitExecutionRepository : IVisitExecutionRepository
{
    public const string CollectionName = "visit_executions";

    /// <summary>WP-VW-W3E (BE-b) — the SCALAR ticks element of <c>StartedAt</c> ([ticks, offset]; the server's UTC clock
    /// writes offset 0): the range filter, the sort and the index key of the evidence flags read.</summary>
    public const string StartedTicksField = "StartedAt.0";

    private readonly IMongoCollection<VisitExecution> _collection;

    public VisitExecutionRepository(IMongoDatabase database)
        => _collection = database.GetCollection<VisitExecution>(CollectionName);

    private static FilterDefinition<VisitExecution> Tenant(Guid tenantId)
        => Builders<VisitExecution>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<VisitExecution?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<VisitExecution>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<VisitExecution?> GetByPlannedVisitIdAsync(Guid tenantId, Guid plannedVisitId, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<VisitExecution>.Filter.Eq(x => x.PlannedVisitId, plannedVisitId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<VisitExecution>> ListByPlannedVisitIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> plannedVisitIds, CancellationToken cancellationToken)
    {
        if (plannedVisitIds is null || plannedVisitIds.Count == 0)
        {
            return Array.Empty<VisitExecution>();
        }

        return await _collection
            .Find(Tenant(tenantId) & Builders<VisitExecution>.Filter.In(x => x.PlannedVisitId, plannedVisitIds))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> InsertAsync(VisitExecution entity, CancellationToken cancellationToken)
    {
        try
        {
            await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false; // the plan already has its execution (the unique index won the race)
        }
    }

    // ── WP-VW-W3E (BE-b, §2.6) — the evidence flags read (ix_visit_executions_tenant_started / _tenant_resource_started) ──

    public static FilterDefinition<VisitExecution> EvidenceFilter(Guid tenantId, VisitEvidenceFlagFilter f)
    {
        var b = Builders<VisitExecution>.Filter;
        var filter = Tenant(tenantId)
                     & b.Gte(StartedTicksField, f.FromInclusive.UtcTicks)
                     & b.Lt(StartedTicksField, f.ToExclusive.UtcTicks);
        if (f.ResourceIds is not null) filter &= b.In(x => x.ResourceId, f.ResourceIds);
        if (f.AnyFlags is { Count: > 0 }) filter &= b.AnyIn(x => x.Flags, f.AnyFlags);
        if (f.OnlyFlagged) filter &= b.Exists("Flags.0");
        if (f.PlannedVisitIds is not null) filter &= b.In(x => x.PlannedVisitId, f.PlannedVisitIds);
        return filter;
    }

    public async Task<(IReadOnlyList<VisitExecution> Items, long Total)> ListForEvidenceFlagsAsync(
        Guid tenantId, VisitEvidenceFlagFilter filter, int skip, int take, CancellationToken cancellationToken)
    {
        var where = EvidenceFilter(tenantId, filter);
        var total = await _collection.CountDocumentsAsync(where, cancellationToken: cancellationToken);
        var items = await _collection.Find(where)
            .Sort(Builders<VisitExecution>.Sort.Descending(StartedTicksField).Descending(x => x.Id))
            .Skip(skip).Limit(take)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<VisitExecution>> ListForEvidenceSummaryAsync(
        Guid tenantId, VisitEvidenceFlagFilter filter, CancellationToken cancellationToken)
        => await _collection.Find(EvidenceFilter(tenantId, filter))
            .Project<VisitExecution>(Builders<VisitExecution>.Projection
                .Include(x => x.Id).Include(x => x.TenantId).Include(x => x.ResourceId).Include(x => x.Flags))
            .ToListAsync(cancellationToken);

    public async Task<bool> ReplaceAsync(VisitExecution entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<VisitExecution>.Filter.Where(
                x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        if (result.IsAcknowledged && result.MatchedCount == 1)
        {
            return true;
        }

        entity.Version = expectedVersion;
        return false;
    }
}

/// <summary>WP-VW-W3 (BE-a, K-W2) — <c>visit_evidence_policies</c>: one document per tenant (unique TenantId). A read
/// that finds none returns null; only the manage PUT inserts it.</summary>
public sealed class VisitEvidencePolicyRepository : IVisitEvidencePolicyRepository
{
    public const string CollectionName = "visit_evidence_policies";

    private readonly IMongoCollection<VisitEvidencePolicy> _collection;

    public VisitEvidencePolicyRepository(IMongoDatabase database)
        => _collection = database.GetCollection<VisitEvidencePolicy>(CollectionName);

    public async Task<VisitEvidencePolicy?> GetAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _collection.Find(Builders<VisitEvidencePolicy>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> InsertAsync(VisitEvidencePolicy entity, CancellationToken cancellationToken)
    {
        try
        {
            await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<bool> ReplaceAsync(VisitEvidencePolicy entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<VisitEvidencePolicy>.Filter.Where(
                x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        if (result.IsAcknowledged && result.MatchedCount == 1)
        {
            return true;
        }

        entity.Version = expectedVersion;
        return false;
    }
}
