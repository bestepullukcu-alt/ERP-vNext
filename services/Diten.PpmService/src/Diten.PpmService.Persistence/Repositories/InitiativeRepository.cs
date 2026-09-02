using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Persistence.Mongo;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.Repositories;


public sealed class InitiativeRepository
    : MongoRepository<Initiative>, IInitiativeRepository, IInitiativeV2Repository
{
    private readonly PpmMongoContext _context;

    public InitiativeRepository(PpmMongoContext context) : base(context, context.Initiatives) => _context = context;

    public async Task<Initiative?> GetActiveSuccessorAsync(Guid tenantId, Guid terminalId, CancellationToken cancellationToken)
    {
        var filter = Builders<Initiative>.Filter.Eq(x => x.TenantId, tenantId)
            & Builders<Initiative>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<Initiative>.Filter.Eq(x => x.SupersedesInitiativeId, terminalId);
        var session = _context.CurrentSession;
        return session is null
            ? await _context.Initiatives.Find(filter).FirstOrDefaultAsync(cancellationToken)
            : await _context.Initiatives.Find(session, filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddClosureAsync(InitiativeClosure closure, CancellationToken cancellationToken)
    {
        if (closure.TenantId == Guid.Empty || closure.InitiativeId == Guid.Empty)
            throw new InvalidOperationException("Initiative closure requires tenant and Initiative identity.");
        await _context.InitiativeClosures.InsertOneAsync(_context.RequireTransaction(), closure,
            cancellationToken: cancellationToken);
    }

    public async Task ClaimTerminalForSuccessorAsync(Guid tenantId, Guid terminalId, Guid successorId, int expectedVersion,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || terminalId == Guid.Empty || successorId == Guid.Empty || expectedVersion < 1)
            throw new ArgumentException("A tenant, terminal Initiative, successor and positive expected version are required.");
        if (terminalId == successorId)
            throw new OptimisticConcurrencyException("An Initiative cannot supersede itself.");

        var filter = Builders<Initiative>.Filter.Eq(x => x.TenantId, tenantId)
            & Builders<Initiative>.Filter.Eq(x => x.Id, terminalId)
            & Builders<Initiative>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<Initiative>.Filter.Eq(x => x.Version, expectedVersion)
            & Builders<Initiative>.Filter.In(x => x.LifecycleState,
                [InitiativeLifecycleState.Completed, InitiativeLifecycleState.Cancelled]);
        var writeFence = Builders<Initiative>.Update.Set(x => x.Version, expectedVersion);
        var result = await _context.Initiatives.UpdateOneAsync(
            _context.RequireTransaction(), filter, writeFence,
            cancellationToken: cancellationToken);
        if (result.MatchedCount != 1)
            throw new OptimisticConcurrencyException(
                "Terminal Initiative was not found in the tenant, is not terminal, or its version changed.");
    }
}
