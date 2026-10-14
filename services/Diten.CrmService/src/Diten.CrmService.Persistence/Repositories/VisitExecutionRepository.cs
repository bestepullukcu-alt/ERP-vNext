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
