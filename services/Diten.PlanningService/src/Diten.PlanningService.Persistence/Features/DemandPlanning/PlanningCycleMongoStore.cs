using Diten.PlanningService.Application.Features.DemandPlanning.Cycles;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

public sealed class PlanningCycleMongoStore : IPlanningCycleStore
{
    private readonly IMongoCollection<PlanningCycle> _cycles;
    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private bool _indexReady;

    public PlanningCycleMongoStore(DemandPlanningMongoContext context) => _cycles = context.PlanningCycles;

    public async Task<CycleInsertResult> InsertOrGetAsync(
        PlanningCycle cycle, CancellationToken cancellationToken)
    {
        await EnsureIndexAsync(cancellationToken);
        var filter = Builders<PlanningCycle>.Filter.Eq(x => x.TenantId, cycle.TenantId) &
                     Builders<PlanningCycle>.Filter.Eq(x => x.LegalEntityId, cycle.LegalEntityId) &
                     Builders<PlanningCycle>.Filter.Eq(x => x.CreatedByActorId, cycle.CreatedByActorId) &
                     Builders<PlanningCycle>.Filter.Eq(x => x.IdempotencyKey, cycle.IdempotencyKey);
        try
        {
            await _cycles.InsertOneAsync(cycle, cancellationToken: cancellationToken);
            return new CycleInsertResult(CycleInsertOutcome.Created, cycle);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await _cycles.Find(filter).FirstOrDefaultAsync(cancellationToken);
            if (existing is null || existing.RequestFingerprint != cycle.RequestFingerprint)
                return new CycleInsertResult(CycleInsertOutcome.Conflict, null);
            return new CycleInsertResult(CycleInsertOutcome.Existing, existing);
        }
    }

    public async Task<PlanningCycle?> ReadAsync(Guid tenantId, Guid legalEntityId,
        Guid cycleId, CancellationToken cancellationToken) =>
        await _cycles.Find(x => x.TenantId == tenantId &&
            x.LegalEntityId == legalEntityId && x.Id == cycleId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        if (_indexReady) return;
        await _indexGate.WaitAsync(cancellationToken);
        try
        {
            if (_indexReady) return;
            var keys = Builders<PlanningCycle>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.LegalEntityId)
                .Ascending(x => x.CreatedByActorId)
                .Ascending(x => x.IdempotencyKey);
            await _cycles.Indexes.CreateOneAsync(
                new CreateIndexModel<PlanningCycle>(keys, new CreateIndexOptions
                {
                    Name = "ux_mod0188_cycle_idempotency_scope", Unique = true
                }), cancellationToken: cancellationToken);
            _indexReady = true;
        }
        finally { _indexGate.Release(); }
    }
}
