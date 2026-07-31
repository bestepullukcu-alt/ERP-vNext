using Diten.Platform.Application.Contracts.Eventing;
using Diten.BuildingBlocks.Eventing;
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

    public async Task<EventOutboxWriteResult> EnqueueAsync(
        EventOutboxWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var candidate = OutboxEvent.FromWriteRequest(request);
        try
        {
            await Collection.InsertOneAsync(candidate, cancellationToken: cancellationToken);
            return EventOutboxWriteResult.Inserted;
        }
        catch (MongoWriteException exception) when (
            exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await GetByEventIdAsync(candidate.EventId, cancellationToken);
            if (existing is not null && existing.HasSameImmutableContent(candidate))
            {
                return EventOutboxWriteResult.Duplicate;
            }

            throw new EventOutboxConflictException(candidate.EventId);
        }
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

    public async Task<EventOutboxPublishItem?> ClaimForPublishAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset stalePublishingCutoffUtc,
        CancellationToken cancellationToken = default)
    {
        var item = await ClaimNextAsync(nowUtc, stalePublishingCutoffUtc, cancellationToken);
        return item is null ? null : ToPublicPublishItem(item);
    }

    public async Task CompletePublishAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<OutboxEvent>.Filter.And(
            Builders<OutboxEvent>.Filter.Eq(x => x.EventId, eventId),
            Builders<OutboxEvent>.Filter.Eq(x => x.Status, OutboxEventStatus.Publishing));
        var update = Builders<OutboxEvent>.Update
            .Set(x => x.Status, OutboxEventStatus.Published)
            .Set(x => x.LastError, null)
            .Set(x => x.NextAttemptAtUtc, null)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        if (result.ModifiedCount != 1)
        {
            throw await CreateStateConflictAsync(eventId, "complete", cancellationToken);
        }
    }

    public async Task FailPublishAsync(
        Guid eventId,
        string error,
        DateTimeOffset nextAttemptAtUtc,
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        var item = await GetByEventIdAsync(eventId, cancellationToken)
                   ?? throw new InvalidOperationException($"Outbox event '{eventId}' was not found.");
        if (item.Status != OutboxEventStatus.Publishing)
        {
            throw new InvalidOperationException($"Outbox event '{eventId}' is not in Publishing state.");
        }

        var nextAttemptCount = item.AttemptCount + 1;
        var isTerminal = nextAttemptCount >= maxAttempts;
        var filter = Builders<OutboxEvent>.Filter.And(
            Builders<OutboxEvent>.Filter.Eq(x => x.EventId, eventId),
            Builders<OutboxEvent>.Filter.Eq(x => x.Status, OutboxEventStatus.Publishing),
            Builders<OutboxEvent>.Filter.Eq(x => x.AttemptCount, item.AttemptCount));
        var update = Builders<OutboxEvent>.Update
            .Set(x => x.Status, isTerminal ? OutboxEventStatus.DeadLettered : OutboxEventStatus.Failed)
            .Set(x => x.LastError, EventErrorRedactor.RedactAndTruncate(error))
            .Set(x => x.NextAttemptAtUtc, isTerminal ? null : nextAttemptAtUtc)
            .Set(x => x.UpdatedAt, DateTime.UtcNow)
            .Inc(x => x.AttemptCount, 1);
        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        if (result.ModifiedCount != 1)
        {
            throw await CreateStateConflictAsync(eventId, "fail", cancellationToken);
        }
    }

    public async Task DeadLetterPublishAsync(
        Guid eventId,
        EventOutboxTerminalFailure failure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(failure);
        var safeError = EventErrorRedactor.RedactAndTruncate(
            $"{failure.Kind}:{failure.ReasonCode}:{failure.SafeDescription}");
        var filter = Builders<OutboxEvent>.Filter.And(
            Builders<OutboxEvent>.Filter.Eq(x => x.EventId, eventId),
            Builders<OutboxEvent>.Filter.Eq(x => x.Status, OutboxEventStatus.Publishing));
        var update = Builders<OutboxEvent>.Update
            .Set(x => x.Status, OutboxEventStatus.DeadLettered)
            .Set(x => x.LastError, safeError)
            .Set(x => x.NextAttemptAtUtc, null)
            .Set(x => x.UpdatedAt, DateTime.UtcNow)
            .Inc(x => x.AttemptCount, 1);
        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        if (result.ModifiedCount == 1)
        {
            return;
        }

        var existing = await GetByEventIdAsync(eventId, cancellationToken)
                       ?? throw new InvalidOperationException($"Outbox event '{eventId}' was not found.");
        if (existing.Status == OutboxEventStatus.DeadLettered
            && string.Equals(existing.LastError, safeError, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Outbox event '{eventId}' cannot transition from {existing.Status} to DeadLettered.");
    }

    private static EventOutboxPublishItem ToPublicPublishItem(OutboxEvent item)
    {
        var metadata = new EventMetadata(
            item.EventId,
            item.EventName,
            item.EventVersion,
            item.CorrelationId,
            item.CausationId,
            item.TenantId,
            item.Producer,
            item.OccurredAtUtc);
        return new EventOutboxPublishItem(
            metadata,
            System.Text.Encoding.UTF8.GetBytes(item.PayloadJson),
            new TrustedTransportMetadata(item.TransportHeaders),
            (EventOutboxDeliveryStatus)(int)item.Status,
            item.AttemptCount,
            item.LastError);
    }

    private async Task<Exception> CreateStateConflictAsync(
        Guid eventId,
        string operation,
        CancellationToken cancellationToken)
    {
        var existing = await GetByEventIdAsync(eventId, cancellationToken);
        return existing is null
            ? new InvalidOperationException($"Outbox event '{eventId}' was not found.")
            : new InvalidOperationException(
                $"Outbox event '{eventId}' cannot {operation} from {existing.Status}.");
    }
}
