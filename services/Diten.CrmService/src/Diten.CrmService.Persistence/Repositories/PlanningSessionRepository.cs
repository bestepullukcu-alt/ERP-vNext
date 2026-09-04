using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0155 FU05 PlanningSession persistence — ONE collection (<c>planning_sessions</c>), tenant scoped,
/// soft-delete aware, and with <b>no delete method</b>: a session is archived (its provenance stays readable). Ordering
/// happens in memory so the DateTimeOffset audit fields are never a server-side sort key (the parallel-arrays 500).
/// Single-document writes are guarded by the optimistic <see cref="EntityBase.Version"/> token; the atomic apply
/// (planning_sessions + planned_visits) is in <see cref="PlanningSessionApplyUnitOfWork"/>.
/// </summary>
public sealed class PlanningSessionRepository : IPlanningSessionRepository
{
    public const string CollectionName = "planning_sessions";

    private readonly IMongoCollection<PlanningSession> _collection;

    public PlanningSessionRepository(IMongoDatabase database)
        => _collection = database.GetCollection<PlanningSession>(CollectionName);

    private static FilterDefinition<PlanningSession> Tenant(Guid tenantId)
        => Builders<PlanningSession>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<PlanningSession?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<PlanningSession>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PlanningSession>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return Ordered(rows);
    }

    public async Task<IReadOnlyList<PlanningSession>> ListByPeriodAndResourceAsync(
        Guid tenantId, Guid cyclePeriodId, string resourceId, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId)
                  & Builders<PlanningSession>.Filter.Eq(x => x.CyclePeriodId, cyclePeriodId)
                  & Builders<PlanningSession>.Filter.Eq(x => x.ResourceId, resourceId))
            .ToListAsync(cancellationToken);
        return Ordered(rows);
    }

    public async Task InsertAsync(PlanningSession entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<bool> ReplaceAsync(
        PlanningSession entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<PlanningSession>.Filter.Where(
                x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.MatchedCount == 1;
    }

    private static IReadOnlyList<PlanningSession> Ordered(IEnumerable<PlanningSession> rows)
        => rows
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToList();
}
