using Diten.BuildingBlocks.Eventing;
using Diten.PpmService.Persistence.Mongo;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.Repositories;

public sealed class PpmEventOutboxStore(PpmMongoContext context) : IEventOutboxStore
{
    private readonly IMongoCollection<PpmEventOutboxDocument> _collection = context.EventOutbox;

    public async Task<EventOutboxWriteResult> EnqueueAsync(
        EventOutboxWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var candidate = FromRequest(request);
        try
        {
            await _collection.InsertOneAsync(candidate, cancellationToken: cancellationToken);
            return EventOutboxWriteResult.Inserted;
        }
        catch (MongoWriteException exception) when (
            exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await GetByEventIdAsync(candidate.EventId, cancellationToken);
            if (existing is not null && HasSameImmutableContent(existing, candidate))
            {
                return EventOutboxWriteResult.Duplicate;
            }

            throw new EventOutboxConflictException(candidate.EventId);
        }
    }

    public async Task<EventOutboxPublishItem?> ClaimForPublishAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset stalePublishingCutoffUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));
        EnsureUtc(stalePublishingCutoffUtc, nameof(stalePublishingCutoffUtc));

        var eligible = Builders<PpmEventOutboxDocument>.Filter.Or(
            Builders<PpmEventOutboxDocument>.Filter.Eq(
                item => item.Status,
                EventOutboxDeliveryStatus.Pending),
            Builders<PpmEventOutboxDocument>.Filter.And(
                Builders<PpmEventOutboxDocument>.Filter.Eq(
                    item => item.Status,
                    EventOutboxDeliveryStatus.Failed),
                Builders<PpmEventOutboxDocument>.Filter.Lte(
                    item => item.NextAttemptAtUtc,
                    nowUtc.UtcDateTime)),
            Builders<PpmEventOutboxDocument>.Filter.And(
                Builders<PpmEventOutboxDocument>.Filter.Eq(
                    item => item.Status,
                    EventOutboxDeliveryStatus.Publishing),
                Builders<PpmEventOutboxDocument>.Filter.Lte(
                    item => item.UpdatedAtUtc,
                    stalePublishingCutoffUtc.UtcDateTime)));
        var update = Builders<PpmEventOutboxDocument>.Update
            .Set(item => item.Status, EventOutboxDeliveryStatus.Publishing)
            .Set(item => item.UpdatedAtUtc, nowUtc.UtcDateTime);
        var claimed = await _collection.FindOneAndUpdateAsync(
            eligible,
            update,
            new FindOneAndUpdateOptions<PpmEventOutboxDocument>
            {
                IsUpsert = false,
                ReturnDocument = ReturnDocument.After,
                Sort = Builders<PpmEventOutboxDocument>.Sort
                    .Ascending(item => item.CreatedAtUtc)
                    .Ascending(item => item.EventId)
            },
            cancellationToken);

        return claimed is null ? null : ToPublishItem(claimed);
    }

    public async Task CompletePublishAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        EnsureEventId(eventId);
        var result = await _collection.UpdateOneAsync(
            item => item.EventId == eventId
                    && item.Status == EventOutboxDeliveryStatus.Publishing,
            Builders<PpmEventOutboxDocument>.Update
                .Set(item => item.Status, EventOutboxDeliveryStatus.Published)
                .Set(item => item.LastError, null)
                .Set(item => item.NextAttemptAtUtc, null)
                .Set(item => item.UpdatedAtUtc, DateTime.UtcNow),
            cancellationToken: cancellationToken);
        if (result.ModifiedCount != 1)
        {
            throw await StateConflictAsync(eventId, "complete", cancellationToken);
        }
    }

    public async Task FailPublishAsync(
        Guid eventId,
        string error,
        DateTimeOffset nextAttemptAtUtc,
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        EnsureEventId(eventId);
        EnsureUtc(nextAttemptAtUtc, nameof(nextAttemptAtUtc));
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        var current = await GetByEventIdAsync(eventId, cancellationToken)
                      ?? throw new InvalidOperationException("Outbox event was not found.");
        if (current.Status != EventOutboxDeliveryStatus.Publishing)
        {
            throw new InvalidOperationException(
                $"Outbox event cannot fail from {current.Status}.");
        }

        var attemptCount = checked(current.AttemptCount + 1);
        var terminal = attemptCount >= maxAttempts;
        var result = await _collection.UpdateOneAsync(
            item => item.EventId == eventId
                    && item.Status == EventOutboxDeliveryStatus.Publishing
                    && item.AttemptCount == current.AttemptCount,
            Builders<PpmEventOutboxDocument>.Update
                .Set(
                    item => item.Status,
                    terminal
                        ? EventOutboxDeliveryStatus.DeadLettered
                        : EventOutboxDeliveryStatus.Failed)
                .Set(item => item.AttemptCount, attemptCount)
                .Set(item => item.LastError, EventErrorRedactor.RedactAndTruncate(error))
                .Set(
                    item => item.NextAttemptAtUtc,
                    terminal ? null : nextAttemptAtUtc.UtcDateTime)
                .Set(item => item.UpdatedAtUtc, DateTime.UtcNow),
            cancellationToken: cancellationToken);
        if (result.ModifiedCount != 1)
        {
            throw await StateConflictAsync(eventId, "fail", cancellationToken);
        }
    }

    public async Task DeadLetterPublishAsync(
        Guid eventId,
        EventOutboxTerminalFailure failure,
        CancellationToken cancellationToken = default)
    {
        EnsureEventId(eventId);
        ArgumentNullException.ThrowIfNull(failure);
        var safeError = EventErrorRedactor.RedactAndTruncate(
            $"{failure.Kind}:{failure.ReasonCode}:{failure.SafeDescription}");
        var result = await _collection.UpdateOneAsync(
            item => item.EventId == eventId
                    && item.Status == EventOutboxDeliveryStatus.Publishing,
            Builders<PpmEventOutboxDocument>.Update
                .Set(item => item.Status, EventOutboxDeliveryStatus.DeadLettered)
                .Inc(item => item.AttemptCount, 1)
                .Set(item => item.LastError, safeError)
                .Set(item => item.NextAttemptAtUtc, null)
                .Set(item => item.UpdatedAtUtc, DateTime.UtcNow),
            cancellationToken: cancellationToken);
        if (result.ModifiedCount == 1)
        {
            return;
        }

        var existing = await GetByEventIdAsync(eventId, cancellationToken)
                       ?? throw new InvalidOperationException("Outbox event was not found.");
        if (existing.Status == EventOutboxDeliveryStatus.DeadLettered
            && string.Equals(existing.LastError, safeError, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Outbox event cannot dead-letter from {existing.Status}.");
    }

    private Task<PpmEventOutboxDocument?> GetByEventIdAsync(
        Guid eventId,
        CancellationToken cancellationToken) =>
        _collection.Find(item => item.EventId == eventId)
            .FirstOrDefaultAsync(cancellationToken)!;

    private static PpmEventOutboxDocument FromRequest(EventOutboxWriteRequest request)
    {
        var metadata = request.Metadata;
        EnsureEventId(metadata.EventId);
        EnsureEventId(metadata.CorrelationId);
        EnsureUtc(metadata.OccurredAtUtc, nameof(metadata.OccurredAtUtc));
        if (metadata.TenantId is null || metadata.TenantId == Guid.Empty
            || string.IsNullOrWhiteSpace(metadata.EventName)
            || metadata.EventVersion <= 0
            || string.IsNullOrWhiteSpace(metadata.Producer)
            || request.CanonicalPayloadUtf8.IsEmpty)
        {
            throw new EventValidationException("PPM outbox event metadata is invalid.");
        }

        return new PpmEventOutboxDocument
        {
            EventId = metadata.EventId,
            EventName = metadata.EventName,
            EventVersion = metadata.EventVersion,
            CorrelationId = metadata.CorrelationId,
            CausationId = metadata.CausationId,
            TenantId = metadata.TenantId.Value,
            Producer = metadata.Producer,
            OccurredAtUtcTicks = metadata.OccurredAtUtc.UtcTicks,
            CanonicalPayloadUtf8 = request.CanonicalPayloadUtf8.ToArray(),
            TransportHeaders = new Dictionary<string, string>(
                request.TransportMetadata.Headers,
                StringComparer.Ordinal),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static EventOutboxPublishItem ToPublishItem(PpmEventOutboxDocument document) =>
        new(
            new EventMetadata(
                document.EventId,
                document.EventName,
                document.EventVersion,
                document.CorrelationId,
                document.CausationId,
                document.TenantId,
                document.Producer,
                new DateTimeOffset(
                    new DateTime(document.OccurredAtUtcTicks, DateTimeKind.Utc))),
            document.CanonicalPayloadUtf8,
            new TrustedTransportMetadata(document.TransportHeaders),
            document.Status,
            document.AttemptCount,
            document.LastError);

    private static bool HasSameImmutableContent(
        PpmEventOutboxDocument left,
        PpmEventOutboxDocument right) =>
        left.EventId == right.EventId
        && left.EventName == right.EventName
        && left.EventVersion == right.EventVersion
        && left.CorrelationId == right.CorrelationId
        && left.CausationId == right.CausationId
        && left.TenantId == right.TenantId
        && left.Producer == right.Producer
        && left.OccurredAtUtcTicks == right.OccurredAtUtcTicks
        && left.CanonicalPayloadUtf8.AsSpan().SequenceEqual(right.CanonicalPayloadUtf8)
        && left.TransportHeaders.Count == right.TransportHeaders.Count
        && left.TransportHeaders.All(pair =>
            right.TransportHeaders.TryGetValue(pair.Key, out var value)
            && string.Equals(pair.Value, value, StringComparison.Ordinal));

    private async Task<Exception> StateConflictAsync(
        Guid eventId,
        string operation,
        CancellationToken cancellationToken)
    {
        var existing = await GetByEventIdAsync(eventId, cancellationToken);
        return existing is null
            ? new InvalidOperationException("Outbox event was not found.")
            : new InvalidOperationException(
                $"Outbox event cannot {operation} from {existing.Status}.");
    }

    private static void EnsureEventId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new EventValidationException("Event identifier must be non-empty.");
        }
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use UTC offset zero.", parameterName);
        }
    }
}
