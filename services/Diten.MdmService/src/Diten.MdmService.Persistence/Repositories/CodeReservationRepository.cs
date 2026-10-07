using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Diten.MdmService.Api.Tests")]

namespace Diten.MdmService.Persistence.Repositories;

public sealed class CodeReservationRepository : ICodeReservationRepository
{
    private const int CounterRaceRecoveryAttempts = 3;
    private const string CollectionName = "mdm_code_reservations";
    private const string CounterCollectionName = "mdm_canonical_code_counters";
    private readonly IMongoCollection<CodeReservation> _collection;
    private readonly IMongoCollection<CanonicalCodeCounter> _counters;
    private readonly IMongoDatabase _database;
    private readonly Guid _tenantId;

    public CodeReservationRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _database = database;
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

    public Task<CodeReservation?> GetByReservationCommandAsync(
        CodeBearingEntityType entityType, string reservationCommandId, CancellationToken cancellationToken = default) =>
        _collection.Find(ActiveTenantFilter
                & Builders<CodeReservation>.Filter.Eq(x => x.EntityType, entityType)
                & Builders<CodeReservation>.Filter.Eq(x => x.ReservationCommandId, reservationCommandId))
            .FirstOrDefaultAsync(cancellationToken)!;

    // LSKU FIX1 B1 (b): the binding path already knows Burned; this is its first caller.
    // LSKU FIX3 5 (c) tests only: a barrier after the burn's "no live row bound" read, inside its transaction.
    internal Func<Task>? AfterBoundRowCheckForTests { get; set; }

    public const string BoundRowExists = CodeReservationBurnCodes.BoundRowExists;
    public const string BurnTransactionUnavailable = CodeReservationBurnCodes.TransactionUnavailable;
    public const string BurnKindNotTransactional = "CODE_RESERVATION_BURN_KIND_NOT_TRANSACTIONAL";

    /*
     * LSKU FIX3 item 5 (c), CT decision (A): the signature is unchanged, the BEHAVIOUR changed. A code is burned only
     * when no live (not deleted) identity row is bound to it — read in the SAME Mongo transaction as the burn's write.
     * That read closes the burn/insert race ONLY because the LSKU insert writes the reservation in its own transaction
     * (LskuRepository.InsertBoundToLiveCodeAsync): a burn and an insert that overlap fail each other with a write
     * conflict, so never a live row with a burned code. LSKU FIX4 item 3: an insert that does NOT write the
     * reservation (a snapshot read does not conflict with a plain insert — write skew) cannot be protected, so a burn of
     * such a kind is refused outright (BoundRowExistsAsync throws). FG FIX1 part B: the FG insert has the touch
     * (FinishedGoodRepository.InsertBoundToLiveCodeAsync), so FG codes may be burned; GSKU / GP stay refused. A row that
     * was cancelled (an LSKU deleted; a Finished Good whose retained cancellation is settled) does not hold its code.
     * Fail-closed: without transaction support the burn is refused (BurnTransactionUnavailable) — never burned outside one.
     */
    public async Task<ReservationOperationResult> BurnIdentityBindingAsync(
        Guid reservationId,
        Guid identityId,
        int expectedVersion,
        string idempotencyKey,
        string actorId,
        string correlationId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var current = await GetByIdAsync(reservationId, cancellationToken);
        if (current is null) return new(false, null, "CODE_RESERVATION_REQUIRED");
        if (current.BindingState == CodeReservationBindingState.Burned && current.ConsumedEntityId == identityId
            && string.Equals(current.LastCommandId, idempotencyKey, StringComparison.Ordinal))
            return new(true, current);
        IClientSessionHandle session;
        try
        {
            session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is NotSupportedException or MongoException)
        {
            return new(false, current, BurnTransactionUnavailable);
        }
        using (session)
        {
            try
            {
                session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary,
                    WriteConcern.WMajority));
                if (await BoundRowExistsAsync(session, current.EntityType, reservationId, identityId, cancellationToken))
                {
                    await session.AbortTransactionAsync(cancellationToken);
                    return new(false, current, BoundRowExists);
                }
                if (AfterBoundRowCheckForTests is { } barrier) await barrier();
                var burned = await ChangeBindingAsync(reservationId, identityId, expectedVersion, idempotencyKey, actorId,
                    correlationId, CodeReservationBindingState.Burned, ProductAuditOperation.CodeBurned, reason, null,
                    cancellationToken, session);
                if (burned.Succeeded) await session.CommitTransactionAsync(cancellationToken);
                else await session.AbortTransactionAsync(cancellationToken);
                return burned;
            }
            catch (Exception exception) when (exception is NotSupportedException
                                              || exception is MongoCommandException { Code: 20 })
            {
                // Transactions need a replica set (a standalone mongod: the driver refuses, or IllegalOperation) —
                // refused, nothing written.
                return new(false, current, BurnTransactionUnavailable);
            }
            catch (MongoException exception) when (exception.HasErrorLabel("TransientTransactionError"))
            {
                // A write conflict with an overlapping identity insert (or another burn): nothing of ours committed.
                return new(false, await GetByIdAsync(reservationId, cancellationToken), "CONCURRENCY_CONFLICT");
            }
        }
    }

    private async Task<bool> BoundRowExistsAsync(IClientSessionHandle session, CodeBearingEntityType entityType,
        Guid reservationId, Guid identityId, CancellationToken cancellationToken)
    {
        // LSKU FIX4 item 3: only a kind whose insert writes its reservation inside the insert's transaction may be
        // burned here. FG FIX1 part B item 2: LSKU and — with FinishedGoodRepository.InsertBoundToLiveCodeAsync — FG.
        // GSKU / GP stay closed until their inserts gain the same touch.
        var collection = entityType switch
        {
            CodeBearingEntityType.Lsku => "mdm_lskus",
            CodeBearingEntityType.FinishedGood => "mdm_finished_goods",
            CodeBearingEntityType.Gsku or CodeBearingEntityType.GlobalProduct =>
                throw new InvalidOperationException(
                    $"{BurnKindNotTransactional}: the {entityType} insert does not yet write its reservation in its own transaction."),
            _ => throw new InvalidOperationException("CODE_RESERVATION_ENTITY_TYPE_INVALID")
        };
        var bound = Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(_tenantId, GuidRepresentation.Standard))
            & Builders<BsonDocument>.Filter.Ne("IsDeleted", true)
            & (Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(identityId, GuidRepresentation.Standard))
               | Builders<BsonDocument>.Filter.Eq("CodeReservationId", new BsonBinaryData(reservationId, GuidRepresentation.Standard)));
        if (entityType != CodeBearingEntityType.FinishedGood)
            return await _database.GetCollection<BsonDocument>(collection).Find(session, bound).AnyAsync(cancellationToken);
        // A Finished Good is retained when its Draft is cancelled: once that cancellation is SETTLED (its audit receipt
        // validated — the same proof as the GSKU blocker) it holds nothing; pending its audit it still holds the code.
        var rows = await _database.GetCollection<FinishedGood>(collection)
            .Find(session, Builders<FinishedGood>.Filter.Eq(x => x.TenantId, _tenantId)
                           & Builders<FinishedGood>.Filter.Ne(x => x.IsDeleted, true)
                           & (Builders<FinishedGood>.Filter.Eq(x => x.Id, identityId)
                              | Builders<FinishedGood>.Filter.Eq(x => x.CodeReservationId, reservationId)))
            .ToListAsync(cancellationToken);
        return rows.Any(row => Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle
            .FinishedGoodDraftCancellationProof.Disposition(row)
            != Diten.MdmService.Domain.ValueObjects.FinishedGoodCancellationDisposition.Cancelled);
    }

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
        CancellationToken cancellationToken,
        IClientSessionHandle? session = null)
    {
        var current = session is null
            ? await GetByIdAsync(reservationId, cancellationToken)
            : await _collection.Find(session, ActiveTenantFilter & Builders<CodeReservation>.Filter.Eq(x => x.Id, reservationId))
                .FirstOrDefaultAsync(cancellationToken);
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

        var updated = session is null
            ? await _collection.FindOneAndUpdateAsync(filter, update,
                new FindOneAndUpdateOptions<CodeReservation> { ReturnDocument = ReturnDocument.After }, cancellationToken)
            : await _collection.FindOneAndUpdateAsync(session, filter, update,
                new FindOneAndUpdateOptions<CodeReservation> { ReturnDocument = ReturnDocument.After }, cancellationToken);

        if (updated is not null)
        {
            return new(true, updated);
        }

        current = session is null
            ? await GetByIdAsync(reservationId, cancellationToken)
            : await _collection.Find(session, ActiveTenantFilter & Builders<CodeReservation>.Filter.Eq(x => x.Id, reservationId))
                .FirstOrDefaultAsync(cancellationToken);
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
    {
        var timestamp = DateTimeOffset.UtcNow;
        return new LocalAuditIntent
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
            TimestampUtc = timestamp,
            TimestampUtcTicksV1 = timestamp.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))),
            SnapshotReference = $"{aggregateType}/{aggregateId:N}/{postVersion}",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = $"{_tenantId:N}:{aggregateType}:{aggregateId:N}:{commandId}"
        };
    }

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
