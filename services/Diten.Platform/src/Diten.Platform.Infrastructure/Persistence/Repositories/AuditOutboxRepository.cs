using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Infrastructure.Persistence.Migrations;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Services.Audit;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

internal sealed class AuditOutboxRepository : IAuditOutboxWriter
    , IAuditOutboxProcessingRepository
{
    private readonly IMongoCollection<AuditOutboxMessage> _collection;
    private readonly IMongoCollection<BsonDocument> _rawCollection;
    private readonly AuditOutboxTemporalMigrationRepository _temporalMigrationRepository;

    public AuditOutboxRepository(
        IMongoDatabase database,
        AuditOutboxTemporalMigrationRepository temporalMigrationRepository)
    {
        _collection = database.GetCollection<AuditOutboxMessage>(AuditCollectionNames.AuditOutbox);
        _rawCollection = database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox);
        _temporalMigrationRepository = temporalMigrationRepository
            ?? throw new ArgumentNullException(nameof(temporalMigrationRepository));
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

        var filter = Builders<BsonDocument>.Filter.Eq(nameof(AuditOutboxMessage.IdempotencyKey), idempotencyKey.Trim());
        var document = await _rawCollection.Find(filter).FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return null;
        }

        var inspection = AuditOutboxTemporalStorageCompatibility.Inspect(document);
        if (inspection.Kind == AuditOutboxTemporalStorageCompatibility.InspectionKind.Malformed)
        {
            throw new InvalidOperationException(inspection.FailureCode);
        }

        try
        {
            var message = BsonSerializer.Deserialize<AuditOutboxMessage>(document);
            AuditOutboxTemporalStorageCompatibility.ValidateForPersistence(message);
            return message;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_ROW_UNSAFE", ex);
        }
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

        var migrationState = await _temporalMigrationRepository.GetAsync(ct);
        if (migrationState is not null
            && migrationState.Phase != AuditOutboxTemporalMigrationState.Phases.CutoverActive)
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_WORKER_FENCED");
        }

        var useScalarClaims = migrationState?.Phase == AuditOutboxTemporalMigrationState.Phases.CutoverActive;

        var claimed = new List<AuditOutboxProcessingItem>(batchSize);
        for (var i = 0; i < batchSize; i++)
        {
            var message = await ClaimNextAsync(maxAttempts, now, processingStaleAfter, useScalarClaims, ct);
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
            Builders<AuditOutboxMessage>.Filter.Eq(x => x.Status, AuditOutboxStatus.Processing),
            BuildSupportedTemporalShapeFilter());
        var update = BuildNextAttemptPipelineUpdate(status, attempts, nextAttemptAtUtc, lastError);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        if (result.MatchedCount != 1)
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_ROW_UNSAFE");
        }
    }

    private async Task<AuditOutboxMessage?> ClaimNextAsync(
        int maxAttempts,
        DateTimeOffset now,
        TimeSpan processingStaleAfter,
        bool useScalarClaims,
        CancellationToken ct)
    {
        var staleProcessingCutoff = now - processingStaleAfter;

        var readyTimeFilter = useScalarClaims
            ? Builders<AuditOutboxMessage>.Filter.Lte(
                x => x.NextAttemptAtUtcTicksV1,
                AuditOutboxTemporalStorageCompatibility.ToUtcTicks(now))
            : Builders<AuditOutboxMessage>.Filter.Lte(x => x.NextAttemptAtUtc, now);
        var staleTimeFilter = useScalarClaims
            ? Builders<AuditOutboxMessage>.Filter.Lte(
                x => x.NextAttemptAtUtcTicksV1,
                AuditOutboxTemporalStorageCompatibility.ToUtcTicks(staleProcessingCutoff))
            : Builders<AuditOutboxMessage>.Filter.Lte(x => x.NextAttemptAtUtc, staleProcessingCutoff);

        var retryReadyFilter = Builders<AuditOutboxMessage>.Filter.And(
            Builders<AuditOutboxMessage>.Filter.In(x => x.Status, [AuditOutboxStatus.Pending, AuditOutboxStatus.Failed]),
            readyTimeFilter,
            Builders<AuditOutboxMessage>.Filter.Lt(x => x.Attempts, maxAttempts));

        var staleProcessingFilter = Builders<AuditOutboxMessage>.Filter.And(
            Builders<AuditOutboxMessage>.Filter.Eq(x => x.Status, AuditOutboxStatus.Processing),
            staleTimeFilter,
            Builders<AuditOutboxMessage>.Filter.Lt(x => x.Attempts, maxAttempts));

        var filter = Builders<AuditOutboxMessage>.Filter.And(
            Builders<AuditOutboxMessage>.Filter.Or(retryReadyFilter, staleProcessingFilter),
            BuildSupportedTemporalShapeFilter());

        var update = BuildNextAttemptPipelineUpdate(
            AuditOutboxStatus.Processing,
            attempts: null,
            now,
            lastError: null,
            preserveLastError: true);

        var sort = useScalarClaims
            ? Builders<AuditOutboxMessage>.Sort
                .Ascending(x => x.CreatedAtUtcTicksV1)
                .Ascending(x => x.Id)
            : Builders<AuditOutboxMessage>.Sort.Ascending(x => x.CreatedAtUtc);
        var options = new FindOneAndUpdateOptions<AuditOutboxMessage>
        {
            Sort = sort,
            ReturnDocument = ReturnDocument.After
        };

        return await _collection.FindOneAndUpdateAsync(filter, update, options, ct);
    }

    private static FilterDefinition<AuditOutboxMessage> BuildSupportedTemporalShapeFilter()
    {
        var current = Builders<AuditOutboxMessage>.Filter.And(
            Builders<AuditOutboxMessage>.Filter.Eq(
                x => x.TemporalStorageVersion,
                AuditOutboxTemporalStorageCompatibility.CurrentVersion),
            Builders<AuditOutboxMessage>.Filter.Ne(x => x.NextAttemptAtUtcTicksV1, null),
            Builders<AuditOutboxMessage>.Filter.Ne(x => x.CreatedAtUtcTicksV1, null),
            new BsonDocumentFilterDefinition<AuditOutboxMessage>(
                new BsonDocument(
                    "$expr",
                    new BsonDocument(
                        "$and",
                        new BsonArray
                        {
                            BuildShadowEqualityExpression(
                                nameof(AuditOutboxMessage.NextAttemptAtUtc),
                                nameof(AuditOutboxMessage.NextAttemptAtUtcTicksV1)),
                            BuildShadowEqualityExpression(
                                nameof(AuditOutboxMessage.CreatedAtUtc),
                                nameof(AuditOutboxMessage.CreatedAtUtcTicksV1))
                        }))));

        var legacy = Builders<AuditOutboxMessage>.Filter.And(
            Builders<AuditOutboxMessage>.Filter.Exists(x => x.TemporalStorageVersion, false),
            Builders<AuditOutboxMessage>.Filter.Exists(x => x.NextAttemptAtUtcTicksV1, false),
            Builders<AuditOutboxMessage>.Filter.Exists(x => x.CreatedAtUtcTicksV1, false));

        return Builders<AuditOutboxMessage>.Filter.Or(current, legacy);
    }

    private static BsonDocument BuildShadowEqualityExpression(string legacyField, string shadowField) =>
        new(
            "$eq",
            new BsonArray
            {
                $"${shadowField}",
                new BsonDocument(
                    "$subtract",
                    new BsonArray
                    {
                        new BsonDocument("$arrayElemAt", new BsonArray { $"${legacyField}", 0 }),
                        new BsonDocument(
                            "$multiply",
                            new BsonArray
                            {
                                new BsonDocument("$arrayElemAt", new BsonArray { $"${legacyField}", 1 }),
                                TimeSpan.TicksPerMinute
                            })
                    })
            });

    private static UpdateDefinition<AuditOutboxMessage> BuildNextAttemptPipelineUpdate(
        AuditOutboxStatus status,
        int? attempts,
        DateTimeOffset nextAttemptAtUtc,
        string? lastError,
        bool preserveLastError = false)
    {
        var set = new BsonDocument
        {
            [nameof(AuditOutboxMessage.Status)] = (int)status,
            [nameof(AuditOutboxMessage.NextAttemptAtUtc)] =
                AuditOutboxTemporalStorageCompatibility.ToLegacyBsonArray(nextAttemptAtUtc),
            [nameof(AuditOutboxMessage.NextAttemptAtUtcTicksV1)] = new BsonDocument(
                "$cond",
                new BsonArray
                {
                    new BsonDocument(
                        "$eq",
                        new BsonArray
                        {
                            $"${nameof(AuditOutboxMessage.TemporalStorageVersion)}",
                            AuditOutboxTemporalStorageCompatibility.CurrentVersion
                        }),
                    AuditOutboxTemporalStorageCompatibility.ToUtcTicks(nextAttemptAtUtc),
                    "$$REMOVE"
                })
        };

        if (attempts.HasValue)
        {
            set[nameof(AuditOutboxMessage.Attempts)] = attempts.Value;
        }

        if (!preserveLastError)
        {
            set[nameof(AuditOutboxMessage.LastError)] = lastError is null ? BsonNull.Value : lastError;
        }

        return new PipelineUpdateDefinition<AuditOutboxMessage>(
            new[] { new BsonDocument("$set", set) });
    }

    private static AuditOutboxMessage ToPersistenceMessage(AuditOutboxWriteRequest request)
    {
        var message = new AuditOutboxMessage
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

        AuditOutboxTemporalStorageCompatibility.ApplyCurrentVersion(message);
        return message;
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
