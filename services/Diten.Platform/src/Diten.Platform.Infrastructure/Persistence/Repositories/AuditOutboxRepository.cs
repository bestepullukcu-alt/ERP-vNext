using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Services.Audit;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

internal sealed class AuditOutboxRepository : IAuditOutboxWriter
    , IAuditOutboxProcessingRepository
{
    private readonly IMongoCollection<AuditOutboxMessage> _collection;

    public AuditOutboxRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<AuditOutboxMessage>(AuditCollectionNames.AuditOutbox);
    }

    public async Task<bool> TryEnqueueAsync(AuditOutboxWriteRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        return await TryInsertAsync(ToPersistenceMessage(request), ct);
    }

    private async Task<bool> TryInsertAsync(AuditOutboxMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        message.ValidateForInsert();

        try
        {
            await _collection.InsertOneAsync(message, cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<AuditOutboxMessage?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Audit outbox idempotency key is required.", nameof(idempotencyKey));
        }

        var filter = Builders<AuditOutboxMessage>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey.Trim());
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<AuditOutboxProcessingItem>> ClaimNextBatchAsync(
        int batchSize,
        int maxAttempts,
        DateTimeOffset now,
        TimeSpan processingStaleAfter,
        CancellationToken ct = default)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Audit outbox batch size must be greater than zero.");
        }

        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Audit outbox max attempts must be greater than zero.");
        }

        if (processingStaleAfter <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(processingStaleAfter), "Audit outbox processing stale-after must be greater than zero.");
        }

        var claimed = new List<AuditOutboxProcessingItem>(batchSize);
        for (var i = 0; i < batchSize; i++)
        {
            var message = await ClaimNextAsync(maxAttempts, now, processingStaleAfter, ct);
            if (message is null)
            {
                break;
            }

            claimed.Add(ToProcessingItem(message));
        }

        return claimed;
    }

    public async Task MarkCompletedAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Audit outbox message id is required.", nameof(id));
        }

        var filter = Builders<AuditOutboxMessage>.Filter.And(
            Builders<AuditOutboxMessage>.Filter.Eq(x => x.Id, id),
            Builders<AuditOutboxMessage>.Filter.Eq(x => x.Status, AuditOutboxStatus.Processing));
        var update = Builders<AuditOutboxMessage>.Update
            .Set(x => x.Status, AuditOutboxStatus.Completed)
            .Set(x => x.LastError, null);

        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public async Task MarkFailedAsync(
        Guid id,
        AuditOutboxStatus status,
        int attempts,
        DateTimeOffset nextAttemptAtUtc,
        string lastError,
        CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Audit outbox message id is required.", nameof(id));
        }

        if (status is not AuditOutboxStatus.Failed and not AuditOutboxStatus.DeadLetter)
        {
            throw new ArgumentException("Audit outbox failure status must be Failed or DeadLetter.", nameof(status));
        }

        if (attempts < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attempts), "Audit outbox attempts cannot be negative.");
        }

        var filter = Builders<AuditOutboxMessage>.Filter.And(
            Builders<AuditOutboxMessage>.Filter.Eq(x => x.Id, id),
            Builders<AuditOutboxMessage>.Filter.Eq(x => x.Status, AuditOutboxStatus.Processing));
        var update = Builders<AuditOutboxMessage>.Update
            .Set(x => x.Status, status)
            .Set(x => x.Attempts, attempts)
            .Set(x => x.NextAttemptAtUtc, nextAttemptAtUtc)
            .Set(x => x.LastError, lastError);

        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    private async Task<AuditOutboxMessage?> ClaimNextAsync(
        int maxAttempts,
        DateTimeOffset now,
        TimeSpan processingStaleAfter,
        CancellationToken ct)
    {
        var staleProcessingCutoff = now - processingStaleAfter;

        var retryReadyFilter = Builders<AuditOutboxMessage>.Filter.And(
            Builders<AuditOutboxMessage>.Filter.In(x => x.Status, [AuditOutboxStatus.Pending, AuditOutboxStatus.Failed]),
            Builders<AuditOutboxMessage>.Filter.Lte(x => x.NextAttemptAtUtc, now),
            Builders<AuditOutboxMessage>.Filter.Lt(x => x.Attempts, maxAttempts));

        var staleProcessingFilter = Builders<AuditOutboxMessage>.Filter.And(
            Builders<AuditOutboxMessage>.Filter.Eq(x => x.Status, AuditOutboxStatus.Processing),
            Builders<AuditOutboxMessage>.Filter.Lte(x => x.NextAttemptAtUtc, staleProcessingCutoff),
            Builders<AuditOutboxMessage>.Filter.Lt(x => x.Attempts, maxAttempts));

        var filter = Builders<AuditOutboxMessage>.Filter.Or(retryReadyFilter, staleProcessingFilter);

        var update = Builders<AuditOutboxMessage>.Update
            .Set(x => x.Status, AuditOutboxStatus.Processing)
            .Set(x => x.NextAttemptAtUtc, now);

        var options = new FindOneAndUpdateOptions<AuditOutboxMessage>
        {
            Sort = Builders<AuditOutboxMessage>.Sort.Ascending(x => x.CreatedAtUtc),
            ReturnDocument = ReturnDocument.After
        };

        return await _collection.FindOneAndUpdateAsync(filter, update, options, ct);
    }

    private static AuditOutboxMessage ToPersistenceMessage(AuditOutboxWriteRequest request)
    {
        return new AuditOutboxMessage
        {
            TenantId = request.TenantId,
            CorrelationId = request.CorrelationId,
            IdempotencyKey = request.IdempotencyKey.Trim(),
            RequestType = request.RequestType.Trim(),
            Operation = request.Operation,
            EntityType = request.EntityType.Trim(),
            EntityId = request.EntityId,
            Payload = request.Payload.ToDictionary(pair => pair.Key, pair => pair.Value)
        };
    }

    private static AuditOutboxProcessingItem ToProcessingItem(AuditOutboxMessage message)
    {
        return new AuditOutboxProcessingItem(
            message.Id,
            message.TenantId,
            message.CorrelationId,
            message.IdempotencyKey,
            message.RequestType,
            message.Operation,
            message.EntityType,
            message.EntityId,
            message.Payload.ToDictionary(pair => pair.Key, pair => pair.Value),
            message.Status,
            message.Attempts,
            message.NextAttemptAtUtc,
            message.CreatedAtUtc);
    }
}
