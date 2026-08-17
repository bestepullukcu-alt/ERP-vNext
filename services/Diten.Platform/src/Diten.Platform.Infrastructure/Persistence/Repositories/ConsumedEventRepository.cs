using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Common.Tenancy;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class ConsumedEventRepository : RepositoryBase<ConsumedEvent>, IConsumedEventRepository
{
    public ConsumedEventRepository(IPlatformDbContext platformDbContext, ITenantContext tenantContext)
        : base(platformDbContext, tenantContext, "consumed_events")
    {
    }

    public async Task<ConsumedEventStartResult> TryStartAsync(ConsumedEvent consumedEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            await Collection.InsertOneAsync(consumedEvent, cancellationToken: cancellationToken);
            return new ConsumedEventStartResult(ConsumedEventStartStatus.Started, consumedEvent);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await GetAsync(consumedEvent.EventId, consumedEvent.ConsumerName, cancellationToken)
                           ?? consumedEvent;

            if (existing.Status == ConsumedEventStatus.Failed)
            {
                await MarkRetryStartedAsync(existing.EventId, existing.ConsumerName, cancellationToken);
                existing.MarkRetryStarted();
                return new ConsumedEventStartResult(ConsumedEventStartStatus.Started, existing);
            }

            var status = existing.Status == ConsumedEventStatus.Consumed || existing.Status == ConsumedEventStatus.SkippedDuplicate
                ? ConsumedEventStartStatus.ConsumedDuplicate
                : ConsumedEventStartStatus.InFlightDuplicate;
            return new ConsumedEventStartResult(status, existing);
        }
    }

    public Task MarkConsumedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
    {
        var update = Builders<ConsumedEvent>.Update
            .Set(x => x.Status, ConsumedEventStatus.Consumed)
            .Set(x => x.ConsumedAtUtc, DateTimeOffset.UtcNow)
            .Set(x => x.LastError, null);

        return Collection.UpdateOneAsync(BuildFilter(eventId, consumerName), update, cancellationToken: cancellationToken);
    }

    public Task MarkSkippedDuplicateAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
    {
        var update = Builders<ConsumedEvent>.Update
            .Set(x => x.Status, ConsumedEventStatus.SkippedDuplicate)
            .Set(x => x.ConsumedAtUtc, DateTimeOffset.UtcNow);

        return Collection.UpdateOneAsync(BuildFilter(eventId, consumerName), update, cancellationToken: cancellationToken);
    }

    public async Task MarkFailedAsync(Guid eventId, string consumerName, string error, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(eventId, consumerName, cancellationToken);
        var nextAttemptCount = (existing?.AttemptCount ?? 0) + 1;
        var update = Builders<ConsumedEvent>.Update
            .Set(x => x.Status, ConsumedEventStatus.Failed)
            .Set(x => x.AttemptCount, nextAttemptCount)
            .Set(x => x.LastError, Diten.BuildingBlocks.Eventing.EventErrorRedactor.RedactAndTruncate(error));

        await Collection.UpdateOneAsync(BuildFilter(eventId, consumerName), update, cancellationToken: cancellationToken);
    }

    public Task<ConsumedEvent?> GetAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
    {
        return Collection.Find(BuildFilter(eventId, consumerName)).FirstOrDefaultAsync(cancellationToken)!;
    }

    private Task MarkRetryStartedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken)
    {
        var update = Builders<ConsumedEvent>.Update
            .Set(x => x.Status, ConsumedEventStatus.Started)
            .Set(x => x.LastError, null);

        return Collection.UpdateOneAsync(BuildFilter(eventId, consumerName), update, cancellationToken: cancellationToken);
    }

    private static FilterDefinition<ConsumedEvent> BuildFilter(Guid eventId, string consumerName)
    {
        return Builders<ConsumedEvent>.Filter.And(
            Builders<ConsumedEvent>.Filter.Eq(x => x.EventId, eventId),
            Builders<ConsumedEvent>.Filter.Eq(x => x.ConsumerName, consumerName));
    }
}
