using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class CodeReservationRepository : ICodeReservationRepository
{
    private const int CounterRaceRecoveryAttempts = 3;
    private const string CollectionName = "mdm_code_reservations";
    private const string CounterCollectionName = "mdm_canonical_code_counters";
    private readonly IMongoCollection<CodeReservation> _collection;
    private readonly IMongoCollection<CanonicalCodeCounter> _counters;
    private readonly Guid _tenantId;

    public CodeReservationRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _collection = database.GetCollection<CodeReservation>(CollectionName);
        _counters = database.GetCollection<CanonicalCodeCounter>(CounterCollectionName);
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<CodeReservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _collection.Find(ActiveTenantFilter & Builders<CodeReservation>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<CodeReservation> ReserveAsync(
        CodeBearingEntityType entityType,
        string idempotencyKey,
        string actorId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindByReservationCommandAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.EntityType != entityType)
            {
                throw new InvalidOperationException("IDEMPOTENCY_KEY_CONFLICT");
            }

            return existing;
        }

        if (await ReservationCommandTombstoneExistsAsync(idempotencyKey, cancellationToken))
        {
            throw new InvalidOperationException("RESERVATION_IDEMPOTENCY_KEY_TOMBSTONED");
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var sequence = await NextSequenceAsync(cancellationToken);
            var code = BuildCode(entityType, sequence);
            var timestamp = DateTimeOffset.UtcNow;
            var reservation = new CodeReservation
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                EntityType = entityType,
                ReservedCode = code,
                ReservationState = CodeReservationState.Reserved,
                BindingState = CodeReservationBindingState.None,
                ReservationCommandId = idempotencyKey,
                LastCommandId = idempotencyKey,
                ReservedAt = timestamp,
                ReservedByActorId = actorId,
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                Version = 0
            };
            reservation.AuditIntents.Add(CreateIntent(
                AuditAggregateType.CodeReservation,
                reservation.Id,
                -1,
                0,
                ProductAuditOperation.CodeReserved,
                actorId,
                correlationId,
                idempotencyKey,
                1,
                $"{_tenantId:N}|{reservation.Id:N}|{code}|{entityType}|RESERVED"));

            try
            {
                await _collection.InsertOneAsync(reservation, cancellationToken: cancellationToken);
                return reservation;
            }
            catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                existing = await FindByReservationCommandAsync(idempotencyKey, cancellationToken);
                if (existing is not null)
                {
                    if (existing.EntityType != entityType)
                    {
                        throw new InvalidOperationException("IDEMPOTENCY_KEY_CONFLICT", exception);
                    }

                    return existing;
                }


                if (await ReservationCommandTombstoneExistsAsync(idempotencyKey, cancellationToken))
                {
                    throw new InvalidOperationException("RESERVATION_IDEMPOTENCY_KEY_TOMBSTONED", exception);
                }
            }
        }

        throw new InvalidOperationException("CANONICAL_CODE_ALLOCATION_FAILED");
    }

    public async Task<ReservationOperationResult> ConsumeForIdentityAsync(
        Guid reservationId,
        CodeBearingEntityType expectedEntityType,
        Guid identityId,
        int expectedVersion,
        string idempotencyKey,
        string actorId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var current = await GetByIdAsync(reservationId, cancellationToken);
        if (current is null)
        {
            return new(false, null, "CODE_RESERVATION_REQUIRED");
        }

        if (current.EntityType != expectedEntityType)
        {
            return new(false, current, "CODE_RESERVATION_MISMATCH");
        }

        if (current.ReservationState == CodeReservationState.Consumed
            && string.Equals(current.ConsumeCommandId, idempotencyKey, StringComparison.Ordinal)
            && current.ConsumedEntityId.HasValue)
        {
            return new(true, current);
        }

        if (current.ReservationState != CodeReservationState.Reserved)
        {
            return new(false, current, "CODE_RESERVATION_ALREADY_TERMINAL");
        }

        var commandConflict = await FindByConsumeCommandAsync(idempotencyKey, cancellationToken);
        if (commandConflict is not null && commandConflict.Id != reservationId)
        {
            return new(false, current, "IDEMPOTENCY_KEY_CONFLICT");
        }

        if (await ConsumeCommandTombstoneExistsAsync(idempotencyKey, cancellationToken))
        {
            return new(false, current, "IDEMPOTENCY_KEY_CONFLICT");
        }

        var identityConflict = await FindByConsumedIdentityAsync(identityId, cancellationToken);
        if (identityConflict is not null && identityConflict.Id != reservationId)
        {
            return new(false, current, "IDENTITY_RESERVATION_CONFLICT");
        }

        if (await ConsumedIdentityTombstoneExistsAsync(identityId, cancellationToken))
        {
            return new(false, current, "IDENTITY_RESERVATION_CONFLICT");
        }

        var timestamp = DateTimeOffset.UtcNow;
        var intent = CreateIntent(
            AuditAggregateType.CodeReservation,
            current.Id,
            expectedVersion,
            expectedVersion + 1,
            ProductAuditOperation.CodeConsumed,
            actorId,
            correlationId,
            idempotencyKey,
            current.AuditIntents.Count + 1L,
            $"{_tenantId:N}|{current.Id:N}|{current.ReservedCode}|{identityId:N}|CONSUMED");

        var filter = ActiveTenantFilter
            & Builders<CodeReservation>.Filter.Eq(x => x.Id, reservationId)
            & Builders<CodeReservation>.Filter.Eq(x => x.Version, expectedVersion)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservationState, CodeReservationState.Reserved)
            // Consume must leave one slot for the mandatory confirm-or-burn resolution intent.
            & Builders<CodeReservation>.Filter.Exists($"AuditIntents.{AuditIntentLimits.MaxPerAggregate - 2}", false);
        var update = Builders<CodeReservation>.Update
            .Set(x => x.ReservationState, CodeReservationState.Consumed)
            .Set(x => x.BindingState, CodeReservationBindingState.PendingIdentityWrite)
            .Set(x => x.ConsumeCommandId, idempotencyKey)
            .Set(x => x.LastCommandId, idempotencyKey)
            .Set(x => x.ConsumedEntityId, identityId)
            .Set(x => x.ConsumedAt, timestamp)
            .Set(x => x.UpdatedAt, timestamp)
            .Inc(x => x.Version, 1)
            .Push(x => x.AuditIntents, intent);

        CodeReservation? updated;
        try
        {
            updated = await _collection.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<CodeReservation> { ReturnDocument = ReturnDocument.After },
                cancellationToken);
        }
        catch (Exception exception) when (IsDuplicateKey(exception))
        {
            commandConflict = await FindByConsumeCommandAsync(idempotencyKey, cancellationToken);
            if (commandConflict is not null && commandConflict.Id != reservationId)
            {
                return new(false, current, "IDEMPOTENCY_KEY_CONFLICT");
            }

            if (await ConsumeCommandTombstoneExistsAsync(idempotencyKey, cancellationToken))
            {
                return new(false, current, "IDEMPOTENCY_KEY_CONFLICT");
            }

            identityConflict = await FindByConsumedIdentityAsync(identityId, cancellationToken);
            if (identityConflict is not null && identityConflict.Id != reservationId)
            {
                return new(false, current, "IDENTITY_RESERVATION_CONFLICT");
            }

            if (await ConsumedIdentityTombstoneExistsAsync(identityId, cancellationToken))
            {
                return new(false, current, "IDENTITY_RESERVATION_CONFLICT");
            }

            throw;
        }

        if (updated is not null)
        {
            return new(true, updated);
        }

        current = await GetByIdAsync(reservationId, cancellationToken);
        if (current?.ReservationState == CodeReservationState.Consumed
            && string.Equals(current.ConsumeCommandId, idempotencyKey, StringComparison.Ordinal)
            && current.ConsumedEntityId.HasValue)
        {
            return new(true, current);
        }

        return new(false, current, current?.Version != expectedVersion
            ? "CONCURRENCY_CONFLICT"
            : current?.AuditIntents.Count >= AuditIntentLimits.MaxPerAggregate - 1
                ? "AUDIT_INTENT_CAPACITY_EXCEEDED"
                : "CODE_RESERVATION_ALREADY_TERMINAL");
    }

    public Task<ReservationOperationResult> ConfirmIdentityBindingAsync(
        Guid reservationId,
        Guid identityId,
        int expectedVersion,
        string idempotencyKey,
        string actorId,
        string correlationId,
        CancellationToken cancellationToken = default)
        => ChangeBindingAsync(
            reservationId,
            identityId,
            expectedVersion,
            idempotencyKey,
            actorId,
            correlationId,
            CodeReservationBindingState.Confirmed,
            ProductAuditOperation.CodeBindingConfirmed,
            null,
            null,
            cancellationToken);

    private async Task<ReservationOperationResult> ChangeBindingAsync(
        Guid reservationId,
        Guid identityId,
        int expectedVersion,
        string idempotencyKey,
        string actorId,
        string correlationId,
        CodeReservationBindingState targetState,
        ProductAuditOperation operation,
        string? reason,
        string? recoveryDisposition,
        CancellationToken cancellationToken)
    {
        var current = await GetByIdAsync(reservationId, cancellationToken);
        if (current is null)
        {
            return new(false, null, "CODE_RESERVATION_REQUIRED");
        }

        if (current.BindingState == targetState
            && current.ConsumedEntityId == identityId
            && string.Equals(current.LastCommandId, idempotencyKey, StringComparison.Ordinal))
        {
            return new(true, current);
        }

        if (current.ReservationState != CodeReservationState.Consumed
            || current.BindingState != CodeReservationBindingState.PendingIdentityWrite
            || current.ConsumedEntityId != identityId)
        {
            return new(false, current, "CODE_RESERVATION_MISMATCH");
        }

        var timestamp = DateTimeOffset.UtcNow;
        var intent = CreateIntent(
            AuditAggregateType.CodeReservation,
            current.Id,
            expectedVersion,
            expectedVersion + 1,
            operation,
            actorId,
            correlationId,
            idempotencyKey,
            current.AuditIntents.Count + 1L,
            $"{_tenantId:N}|{current.Id:N}|{current.ReservedCode}|{identityId:N}|{targetState}|{reason}");

        var filter = ActiveTenantFilter
            & Builders<CodeReservation>.Filter.Eq(x => x.Id, reservationId)
            & Builders<CodeReservation>.Filter.Eq(x => x.Version, expectedVersion)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservationState, CodeReservationState.Consumed)
            & Builders<CodeReservation>.Filter.Eq(x => x.BindingState, CodeReservationBindingState.PendingIdentityWrite)
            & Builders<CodeReservation>.Filter.Eq(x => x.ConsumedEntityId, identityId)
            & Builders<CodeReservation>.Filter.Exists($"AuditIntents.{AuditIntentLimits.MaxPerAggregate - 1}", false);
        var update = Builders<CodeReservation>.Update
            .Set(x => x.BindingState, targetState)
            .Set(x => x.LastCommandId, idempotencyKey)
            .Set(x => x.UpdatedAt, timestamp)
            .Set(x => x.BindingConfirmedAt, targetState == CodeReservationBindingState.Confirmed ? timestamp : null)
            .Set(x => x.BurnedAt, targetState == CodeReservationBindingState.Burned ? timestamp : null)
            .Set(x => x.BurnReason, reason)
            .Set(x => x.RecoveryDisposition, recoveryDisposition)
            .Inc(x => x.Version, 1)
            .Push(x => x.AuditIntents, intent);

        var updated = await _collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<CodeReservation> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        if (updated is not null)
        {
            return new(true, updated);
        }

        current = await GetByIdAsync(reservationId, cancellationToken);
        if (current?.BindingState == targetState
            && current.ConsumedEntityId == identityId
            && string.Equals(current.LastCommandId, idempotencyKey, StringComparison.Ordinal))
        {
            return new(true, current);
        }

        return new(false, current, current?.Version != expectedVersion
            ? "CONCURRENCY_CONFLICT"
            : current?.AuditIntents.Count >= AuditIntentLimits.MaxPerAggregate
                ? "AUDIT_INTENT_CAPACITY_EXCEEDED"
                : "CODE_RESERVATION_MISMATCH");
    }

    private async Task<CodeReservation?> FindByReservationCommandAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
        => await _collection.Find(ActiveTenantFilter
                & Builders<CodeReservation>.Filter.Eq(x => x.ReservationCommandId, idempotencyKey))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<CodeReservation?> FindByConsumeCommandAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
        => await _collection.Find(ActiveTenantFilter
                & Builders<CodeReservation>.Filter.Eq(x => x.ConsumeCommandId, idempotencyKey))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<CodeReservation?> FindByConsumedIdentityAsync(
        Guid identityId,
        CancellationToken cancellationToken)
        => await _collection.Find(ActiveTenantFilter
                & Builders<CodeReservation>.Filter.Eq(x => x.ConsumedEntityId, identityId))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<bool> ReservationCommandTombstoneExistsAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
        => await _collection.Find(
                TombstoneTenantFilter
                & Builders<CodeReservation>.Filter.Eq(x => x.ReservationCommandId, idempotencyKey))
            .AnyAsync(cancellationToken);

    private async Task<bool> ConsumeCommandTombstoneExistsAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
        => await _collection.Find(
                TombstoneTenantFilter
                & Builders<CodeReservation>.Filter.Eq(x => x.ConsumeCommandId, idempotencyKey))
            .AnyAsync(cancellationToken);

    private async Task<bool> ConsumedIdentityTombstoneExistsAsync(
        Guid identityId,
        CancellationToken cancellationToken)
        => await _collection.Find(
                TombstoneTenantFilter
                & Builders<CodeReservation>.Filter.Eq(x => x.ConsumedEntityId, identityId))
            .AnyAsync(cancellationToken);

    private async Task<long> NextSequenceAsync(CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var filter = Builders<CanonicalCodeCounter>.Filter.Eq(x => x.TenantId, _tenantId)
            & Builders<CanonicalCodeCounter>.Filter.Eq(x => x.IsDeleted, false);
        var update = Builders<CanonicalCodeCounter>.Update
            .SetOnInsert(x => x.Id, Guid.NewGuid())
            .SetOnInsert(x => x.TenantId, _tenantId)
            .SetOnInsert(x => x.IsDeleted, false)
            .SetOnInsert(x => x.DeletedAt, null)
            .SetOnInsert(x => x.CreatedAt, timestamp)
            .Set(x => x.UpdatedAt, timestamp)
            .Inc(x => x.NextSequence, 1)
            .Inc(x => x.Version, 1);

        try
        {
            var updated = await _counters.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<CanonicalCodeCounter>
                {
                    IsUpsert = true,
                    ReturnDocument = ReturnDocument.After
                },
                cancellationToken);
            return updated.NextSequence;
        }
        catch (Exception exception) when (IsDuplicateKey(exception))
        {
            // Another caller created this tenant's counter after our upsert filter was evaluated.
            // Recovery must increment the winning document without another upsert attempt.
        }

        for (var attempt = 1; attempt <= CounterRaceRecoveryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var recovered = await _counters.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<CanonicalCodeCounter>
                {
                    IsUpsert = false,
                    ReturnDocument = ReturnDocument.After
                },
                cancellationToken);
            if (recovered is not null)
            {
                return recovered.NextSequence;
            }

            await Task.Yield();
        }

        throw new InvalidOperationException("CANONICAL_CODE_COUNTER_RECOVERY_FAILED");
    }

    private static string BuildCode(CodeBearingEntityType entityType, long sequence)
    {
        var prefix = entityType switch
        {
            CodeBearingEntityType.GlobalProduct => "GP",
            CodeBearingEntityType.Gsku => "GS",
            CodeBearingEntityType.Lsku => "LS",
            CodeBearingEntityType.FinishedGood => "FG",
            _ => throw new ArgumentOutOfRangeException(nameof(entityType))
        };

        return $"{prefix}-{sequence:D12}";
    }

    private LocalAuditIntent CreateIntent(
        AuditAggregateType aggregateType,
        Guid aggregateId,
        int preVersion,
        int postVersion,
        ProductAuditOperation operation,
        string actorId,
        string correlationId,
        string commandId,
        long sequence,
        string evidence)
        => new()
        {
            IntentId = Guid.NewGuid(),
            TenantId = _tenantId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            PreVersion = preVersion,
            PostVersion = postVersion,
            Operation = operation,
            ActorId = actorId,
            CorrelationId = correlationId,
            CausationId = commandId,
            CommandId = commandId,
            Sequence = sequence,
            TimestampUtc = DateTimeOffset.UtcNow,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))),
            SnapshotReference = $"{aggregateType}/{aggregateId:N}/{postVersion}",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = $"{_tenantId:N}:{aggregateType}:{aggregateId:N}:{commandId}"
        };

    private void EnsureIndexes()
    {
        var models = new[]
        {
            new CreateIndexModel<CodeReservation>(
                Builders<CodeReservation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ReservedCode),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_code_reservations_tenant_code" }),
            new CreateIndexModel<CodeReservation>(
                Builders<CodeReservation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ReservationCommandId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_code_reservations_tenant_reserve_command" }),
            new CreateIndexModel<CodeReservation>(
                Builders<CodeReservation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ConsumedEntityId),
                new CreateIndexOptions { Name = "ix_mdm_code_reservations_tenant_consumed_entity" }),
            new CreateIndexModel<CodeReservation>(
                Builders<CodeReservation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ConsumedEntityId),
                new CreateIndexOptions<CodeReservation>
                {
                    Unique = true,
                    Name = "ux_mdm_code_reservations_tenant_consumed_entity_non_null",
                    PartialFilterExpression = Builders<CodeReservation>.Filter.Type(
                        x => x.ConsumedEntityId,
                        BsonType.Binary)
                }),
            new CreateIndexModel<CodeReservation>(
                Builders<CodeReservation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ConsumeCommandId),
                new CreateIndexOptions<CodeReservation>
                {
                    Unique = true,
                    Name = "ux_mdm_code_reservations_tenant_consume_command_non_null",
                    PartialFilterExpression = Builders<CodeReservation>.Filter.Type(
                        x => x.ConsumeCommandId,
                        BsonType.String)
                })
        };
        _collection.Indexes.CreateMany(models);

        _counters.Indexes.CreateOne(new CreateIndexModel<CanonicalCodeCounter>(
            Builders<CanonicalCodeCounter>.IndexKeys.Ascending(x => x.TenantId),
            new CreateIndexOptions { Unique = true, Name = "ux_mdm_canonical_code_counters_tenant" }));
    }

    private FilterDefinition<CodeReservation> ActiveTenantFilter =>
        Builders<CodeReservation>.Filter.Eq(x => x.TenantId, _tenantId)
        & Builders<CodeReservation>.Filter.Eq(x => x.IsDeleted, false);

    private FilterDefinition<CodeReservation> TombstoneTenantFilter =>
        Builders<CodeReservation>.Filter.Eq(x => x.TenantId, _tenantId)
        & Builders<CodeReservation>.Filter.Eq(x => x.IsDeleted, true);

    private static bool IsDuplicateKey(Exception exception)
        => exception is MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey }
            || exception is MongoCommandException { Code: 11000 };

    private sealed class CanonicalCodeCounter : EntityBase
    {
        public long NextSequence { get; set; }
    }
}
