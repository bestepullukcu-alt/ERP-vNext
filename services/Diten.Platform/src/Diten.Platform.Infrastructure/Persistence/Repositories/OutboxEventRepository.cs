using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Common.Tenancy;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class OutboxEventRepository : RepositoryBase<OutboxEvent>, IOutboxEventRepository, IOutboxObservabilityReader
{
    public OutboxEventRepository(IPlatformDbContext platformDbContext, ITenantContext tenantContext)
        : base(platformDbContext, tenantContext, PlatformCollections.OutboxEvents)
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

    public Task<OutboxEvent?> ClaimNextAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset stalePublishingCutoffUtc,
        CancellationToken cancellationToken = default)
    {
        var retryReadyFilter = Builders<OutboxEvent>.Filter.And(
            Builders<OutboxEvent>.Filter.Eq(x => x.Status, OutboxEventStatus.Failed),
            Builders<OutboxEvent>.Filter.Lte(x => x.NextAttemptAtUtc, nowUtc));
        var stalePublishingFilter = Builders<OutboxEvent>.Filter.And(
            Builders<OutboxEvent>.Filter.Eq(x => x.Status, OutboxEventStatus.Publishing),
            Builders<OutboxEvent>.Filter.Lte(x => x.UpdatedAt, stalePublishingCutoffUtc.UtcDateTime));
        var filter = Builders<OutboxEvent>.Filter.Or(
            Builders<OutboxEvent>.Filter.Eq(x => x.Status, OutboxEventStatus.Pending),
            retryReadyFilter,
            stalePublishingFilter);

        var update = Builders<OutboxEvent>.Update
            .Set(x => x.Status, OutboxEventStatus.Publishing)
            .Set(x => x.UpdatedAt, nowUtc.UtcDateTime);

        var options = new FindOneAndUpdateOptions<OutboxEvent>
        {
            IsUpsert = false,
            ReturnDocument = ReturnDocument.After,
            Sort = Builders<OutboxEvent>.Sort.Ascending(x => x.CreatedAt)
        };

        return Collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
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
