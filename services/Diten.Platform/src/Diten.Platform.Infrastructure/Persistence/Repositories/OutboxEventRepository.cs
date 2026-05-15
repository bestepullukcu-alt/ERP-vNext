using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Common.Tenancy;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class OutboxEventRepository : RepositoryBase<OutboxEvent>, IOutboxEventRepository, IOutboxObservabilityReader
{
    public OutboxEventRepository(IPlatformDbContext platformDbContext, ITenantContext tenantContext)
        : base(platformDbContext, tenantContext, "outbox_events")
    {
    }

    public Task AddAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken = default)
    {
        return Collection.InsertOneAsync(outboxEvent, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxEvent>> GetPendingAsync(DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken = default)
    {
        var filter = Builders<OutboxEvent>.Filter.Or(
            Builders<OutboxEvent>.Filter.Eq(x => x.Status, OutboxEventStatus.Pending),
            Builders<OutboxEvent>.Filter.And(
                Builders<OutboxEvent>.Filter.Eq(x => x.Status, OutboxEventStatus.Failed),
                Builders<OutboxEvent>.Filter.Lte(x => x.NextAttemptAtUtc, nowUtc)));

        return await Collection
            .Find(filter)
            .SortBy(x => x.CreatedAt)
            .Limit(batchSize)
            .ToListAsync(cancellationToken);
    }

    public Task<long> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var filter = Builders<OutboxEvent>.Filter.Or(
            Builders<OutboxEvent>.Filter.Eq(x => x.Status, OutboxEventStatus.Pending),
            Builders<OutboxEvent>.Filter.And(
                Builders<OutboxEvent>.Filter.Eq(x => x.Status, OutboxEventStatus.Failed),
                Builders<OutboxEvent>.Filter.Lte(x => x.NextAttemptAtUtc, nowUtc)));

        return Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    public Task UpdateAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken = default)
    {
        return Collection.ReplaceOneAsync(x => x.Id == outboxEvent.Id, outboxEvent, cancellationToken: cancellationToken);
    }

    public Task<OutboxEvent?> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return Collection.Find(x => x.EventId == eventId).FirstOrDefaultAsync(cancellationToken)!;
    }
}
