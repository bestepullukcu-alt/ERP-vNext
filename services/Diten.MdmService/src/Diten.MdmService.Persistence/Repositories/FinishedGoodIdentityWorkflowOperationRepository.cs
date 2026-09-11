using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class FinishedGoodIdentityWorkflowOperationRepository
    : IFinishedGoodIdentityWorkflowOperationRepository
{
    public const string CollectionName = "mdm_finished_good_identity_workflow_operations";
    private const int MaximumBsonDocumentBytes = 1024 * 1024;
    private const long DotNetUnixEpochUtcTicks = 621355968000000000L;
    private const string MutationEvaluatedAtUtcField = "__p1aMutationEvaluatedAtUtc";
    private const int MaximumPhysicalMutationTransactionAttempts = 7;
    private const int InitialPhysicalMutationRetryDelayMilliseconds = 50;
    private readonly IMongoCollection<FinishedGoodIdentityWorkflowOperation> _operations;
    private readonly IMongoCollection<BsonDocument> _documents;
    private readonly Guid _tenantId;

    public FinishedGoodIdentityWorkflowOperationRepository(
        IMongoDatabase database,
        ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(tenantContext);

        _operations = database.GetCollection<FinishedGoodIdentityWorkflowOperation>(CollectionName);
        _documents = database.GetCollection<BsonDocument>(CollectionName);
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<FinishedGoodIdentityWorkflowReserveResult> ReserveAsync(
        FinishedGoodIdentityWorkflowReservation reservation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (!IsValidReservation(reservation))
        {
            return new(false, false, null, "FINISHED_GOOD_IDENTITY_WORKFLOW_OPERATION_INVALID");
        }

        var nowUtcTicks = UtcNowTicks();
        var operation = new FinishedGoodIdentityWorkflowOperation
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            IsDeleted = false,
            DeletedAt = null,
            Version = 0,
            CreatedAt = new DateTimeOffset(nowUtcTicks, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(nowUtcTicks, TimeSpan.Zero),
            OperationId = reservation.OperationId,
            FinishedGoodId = reservation.FinishedGoodId,
            GskuId = reservation.GskuId,
            ProductDefinitionRevisionId = reservation.ProductDefinitionRevisionId,
            MakerSubjectId = reservation.MakerSubjectId,
            ExpectedFinishedGoodVersion = reservation.ExpectedFinishedGoodVersion,
            StartIdempotencyKey = reservation.StartIdempotencyKey,
            OperationFingerprint = reservation.OperationFingerprint,
            AdmissionScopeSnapshot = reservation.AdmissionScopeSnapshot,
            DueAtUtcTicksV1 = reservation.DueAtUtcTicksV1,
            NextAttemptAtUtcTicksV1 = null,
            LastFailureCode = null,
            LeaseOwner = null,
            LeaseUntilUtcTicksV1 = null,
            LeaseGeneration = 0,
            Checkpoint = FinishedGoodIdentityWorkflowCheckpoint.Prepared,
            RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None,
            TemporalStorageVersion = FinishedGoodIdentityWorkflowOperation.CurrentTemporalStorageVersion,
            CreatedAtUtcTicksV1 = nowUtcTicks,
            UpdatedAtUtcTicksV1 = nowUtcTicks
        };

        if (!FitsMongoDocument(operation))
        {
            return new(false, false, null, "FINISHED_GOOD_IDENTITY_WORKFLOW_OPERATION_TOO_LARGE");
        }

        try
        {
            await _operations.InsertOneAsync(operation, cancellationToken: cancellationToken);
            return new(true, false, operation, null);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return await ReadDuplicateReservationAsync(operation, cancellationToken);
        }
        catch (MongoWriteException exception) when (IsDriverWrappedUnsatisfiableWriteConcern(exception))
        {
            // MongoDB.Driver 2.27 surfaces this particular ambiguous InsertOne result as a
            // MongoWriteException wrapping MongoBulkWriteException<T>, rather than as
            // MongoWriteConcernException. It is not a general write-error fallback.
            var existing = await FindReservationIdentityAsync(operation, cancellationToken);
            if (existing is not null && IsExactReplay(existing, operation))
            {
                return new(true, true, existing, null);
            }

            throw;
        }
        catch (MongoWriteConcernException)
        {
            // A write-concern outcome is ambiguous; success requires tenant-scoped exact persisted read-back.
            var existing = await FindReservationIdentityAsync(operation, cancellationToken);
            if (existing is not null && IsExactReplay(existing, operation))
            {
                return new(true, true, existing, null);
            }

            throw;
        }
    }

    public async Task<FinishedGoodIdentityWorkflowOperation?> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty)
        {
            return null;
        }

        var persisted = await _documents.Find(ActiveTenantDocumentFilter
            & Builders<BsonDocument>.Filter.Eq(nameof(FinishedGoodIdentityWorkflowOperation.OperationId),
                StandardGuid(operationId))).FirstOrDefaultAsync(cancellationToken);
        return persisted is null ? null : DeserializeAndValidate(persisted);
    }

    public async Task<FinishedGoodIdentityWorkflowOperation?> GetByStartIdempotencyKeyAsync(
        string startIdempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!IsExactBounded(startIdempotencyKey, 256))
        {
            return null;
        }

        var persisted = await _documents.Find(ActiveTenantDocumentFilter
            & Builders<BsonDocument>.Filter.Eq(nameof(FinishedGoodIdentityWorkflowOperation.StartIdempotencyKey),
                startIdempotencyKey)).FirstOrDefaultAsync(cancellationToken);
        return persisted is null ? null : DeserializeAndValidate(persisted);
    }

    public async Task<FinishedGoodIdentityWorkflowClaim?> TryClaimAsync(
        FinishedGoodIdentityWorkflowClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OperationId == Guid.Empty
            || !IsLowerHex(request.OperationFingerprint, 64)
            || request.EligibleCheckpoints is not { Count: > 0 and <= 16 }
            || request.EligibleCheckpoints.Any(IsTerminalCheckpoint)
            || request.EligibleCheckpoints.Any(checkpoint => !Enum.IsDefined(checkpoint))
            || !IsExactBounded(request.LeaseOwner, 128)
            || request.ExpectedLeaseGeneration < 0
            || request.LeaseDurationTicks is <= 0 or > TimeSpan.TicksPerMinute * 5)
        {
            return null;
        }

        var leaseDurationTicks = checked(
            ((request.LeaseDurationTicks + TimeSpan.TicksPerMillisecond - 1) / TimeSpan.TicksPerMillisecond)
            * TimeSpan.TicksPerMillisecond);
        var eligible = BuildClaimDocumentFilter(request);
        var beforeDocument = await _documents.Find(eligible).FirstOrDefaultAsync(cancellationToken);
        if (beforeDocument is null)
        {
            return null;
        }

        var before = DeserializeAndValidate(beforeDocument);
        return await ClaimAtPhysicalServerTimeAsync(
            request,
            beforeDocument,
            before,
            leaseDurationTicks,
            cancellationToken);
    }

    public async Task<FinishedGoodIdentityWorkflowRecoverablePage> DiscoverRecoverableAsync(
        int limit,
        FinishedGoodIdentityWorkflowRecoveryCursor? after = null,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100 || after?.OperationId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var filter = BuildDiscoverableDocumentFilter();
        if (after is not null)
        {
            filter &= after.NextAttemptAtUtcTicksV1.HasValue
                ? Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Gt(
                        nameof(FinishedGoodIdentityWorkflowOperation.NextAttemptAtUtcTicksV1),
                        after.NextAttemptAtUtcTicksV1),
                    Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Eq(
                            nameof(FinishedGoodIdentityWorkflowOperation.NextAttemptAtUtcTicksV1),
                            after.NextAttemptAtUtcTicksV1),
                        Builders<BsonDocument>.Filter.Gt(
                            nameof(FinishedGoodIdentityWorkflowOperation.OperationId), StandardGuid(after.OperationId))))
                : Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Ne(
                        nameof(FinishedGoodIdentityWorkflowOperation.NextAttemptAtUtcTicksV1), BsonNull.Value),
                    Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Eq(
                            nameof(FinishedGoodIdentityWorkflowOperation.NextAttemptAtUtcTicksV1), BsonNull.Value),
                        Builders<BsonDocument>.Filter.Gt(
                            nameof(FinishedGoodIdentityWorkflowOperation.OperationId), StandardGuid(after.OperationId))));
        }

        var documents = await _documents.Find(filter)
            .Sort(Builders<BsonDocument>.Sort
                .Ascending(nameof(FinishedGoodIdentityWorkflowOperation.NextAttemptAtUtcTicksV1))
                .Ascending(nameof(FinishedGoodIdentityWorkflowOperation.OperationId)))
            .Limit(limit)
            .ToListAsync(cancellationToken);
        var rows = documents.Select(DeserializeAndValidate).ToArray();
        return new(
            rows,
            rows.Length == limit
                ? new FinishedGoodIdentityWorkflowRecoveryCursor(rows[^1].NextAttemptAtUtcTicksV1, rows[^1].OperationId)
                : null);
    }

    public async Task<FinishedGoodIdentityWorkflowAdvanceResult> AdvanceAsync(
        FinishedGoodIdentityWorkflowClaim claim,
        FinishedGoodIdentityWorkflowCheckpointMutation mutation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(mutation);
        if (claim.TenantId != _tenantId
            || claim.OperationId == Guid.Empty
            || !IsLowerHex(claim.OperationFingerprint, 64)
            || !IsExactBounded(claim.LeaseOwner, 128)
            || claim.LeaseGeneration < 1
            || !Enum.IsDefined(claim.Checkpoint)
            || !Enum.IsDefined(mutation.NextCheckpoint)
            || IsTerminalCheckpoint(claim.Checkpoint)
            || !IsAllowedTransition(claim.Checkpoint, mutation.NextCheckpoint)
            || !IsCoherentMutation(claim.Checkpoint, mutation))
        {
            return new(false, null, "FINISHED_GOOD_IDENTITY_WORKFLOW_ADVANCE_INVALID");
        }

        var physicalFilter = BuildAdvanceDocumentFilter(claim);
        var beforeDocument = await _documents.Find(physicalFilter).FirstOrDefaultAsync(cancellationToken);
        if (beforeDocument is null)
        {
            return new(false, null, "FINISHED_GOOD_IDENTITY_WORKFLOW_STALE_CLAIM");
        }

        var before = DeserializeAndValidate(beforeDocument);
        return await AdvanceAtPhysicalServerTimeAsync(
            claim,
            mutation,
            physicalFilter,
            beforeDocument,
            before,
            cancellationToken);
    }

    private async Task<FinishedGoodIdentityWorkflowClaim?> ClaimAtPhysicalServerTimeAsync(
        FinishedGoodIdentityWorkflowClaimRequest request,
        BsonDocument beforeDocument,
        FinishedGoodIdentityWorkflowOperation before,
        long leaseDurationTicks,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumPhysicalMutationTransactionAttempts; attempt++)
        {
            try
            {
                return await ClaimTransactionAttemptAsync(
                    request,
                    beforeDocument,
                    before,
                    leaseDurationTicks,
                    cancellationToken);
            }
            catch (MongoException exception) when (
                IsRetryablePhysicalMutationTransaction(exception) && !cancellationToken.IsCancellationRequested)
            {
                if (attempt == MaximumPhysicalMutationTransactionAttempts)
                {
                    return null;
                }

                await Task.Delay(PhysicalMutationRetryDelay(attempt), cancellationToken);
            }
        }

        return null;
    }

    private async Task<FinishedGoodIdentityWorkflowClaim?> ClaimTransactionAttemptAsync(
        FinishedGoodIdentityWorkflowClaimRequest request,
        BsonDocument beforeDocument,
        FinishedGoodIdentityWorkflowOperation before,
        long leaseDurationTicks,
        CancellationToken cancellationToken)
    {
        using var session = await _documents.Database.Client.StartSessionAsync(
            cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            var claimCandidateFields = new BsonDocument
            {
                { nameof(FinishedGoodIdentityWorkflowOperation.LeaseOwner), request.LeaseOwner },
                { nameof(FinishedGoodIdentityWorkflowOperation.LeaseUntilUtcTicksV1), new BsonInt64(0) },
                { nameof(FinishedGoodIdentityWorkflowOperation.LeaseGeneration), before.LeaseGeneration + 1 },
                { nameof(FinishedGoodIdentityWorkflowOperation.UpdatedAtUtcTicksV1), before.UpdatedAtUtcTicksV1 },
                { nameof(FinishedGoodIdentityWorkflowOperation.UpdatedAt),
                    beforeDocument[nameof(FinishedGoodIdentityWorkflowOperation.UpdatedAt)] },
                { nameof(FinishedGoodIdentityWorkflowOperation.Version), before.Version + 1 },
                { MutationEvaluatedAtUtcField, new BsonDateTime(0) }
            };
            var claimFilter = BuildClaimDocumentFilter(request)
                              & Builders<BsonDocument>.Filter.Eq(
                                  nameof(FinishedGoodIdentityWorkflowOperation.Version), before.Version)
                              & Builders<BsonDocument>.Filter.Eq(
                                  nameof(FinishedGoodIdentityWorkflowOperation.AdmissionScopeSnapshot),
                                  beforeDocument[nameof(FinishedGoodIdentityWorkflowOperation.AdmissionScopeSnapshot)])
                              & ProjectedDocumentFitsFilter(ProjectedRoot(claimCandidateFields));
            var claimedWithoutLease = await _documents.FindOneAndUpdateAsync(
                session,
                claimFilter,
                new BsonDocumentUpdateDefinition<BsonDocument>(new BsonDocument
                {
                    { "$set", new BsonDocument(
                        nameof(FinishedGoodIdentityWorkflowOperation.LeaseOwner), request.LeaseOwner) },
                    { "$inc", new BsonDocument
                    {
                        { nameof(FinishedGoodIdentityWorkflowOperation.LeaseGeneration), 1 },
                        { nameof(FinishedGoodIdentityWorkflowOperation.Version), 1 }
                    } },
                    { "$currentDate", new BsonDocument(MutationEvaluatedAtUtcField, true) }
                }),
                ReturnAfterOptions,
                cancellationToken);
            if (claimedWithoutLease is null)
            {
                await session.AbortTransactionAsync(CancellationToken.None);
                return null;
            }

            DeserializeAndValidate(claimedWithoutLease);
            var eligibilityEvaluatedAtUtcTicks = ServerDateTicks(claimedWithoutLease);
            var leaseUntilUtcTicks = checked(eligibilityEvaluatedAtUtcTicks + leaseDurationTicks);
            var claimFields = new BsonDocument
            {
                { nameof(FinishedGoodIdentityWorkflowOperation.LeaseOwner), request.LeaseOwner },
                { nameof(FinishedGoodIdentityWorkflowOperation.LeaseUntilUtcTicksV1), leaseUntilUtcTicks },
                { nameof(FinishedGoodIdentityWorkflowOperation.LeaseGeneration), before.LeaseGeneration + 1 },
                { nameof(FinishedGoodIdentityWorkflowOperation.UpdatedAtUtcTicksV1), eligibilityEvaluatedAtUtcTicks },
                { nameof(FinishedGoodIdentityWorkflowOperation.UpdatedAt), DateTimeOffsetArray(eligibilityEvaluatedAtUtcTicks) },
                { nameof(FinishedGoodIdentityWorkflowOperation.Version), before.Version + 1 },
                { MutationEvaluatedAtUtcField, new BsonDateTime(0) }
            };
            var leaseFilter = ActiveTenantDocumentFilter
                              & Builders<BsonDocument>.Filter.Eq(
                                  nameof(FinishedGoodIdentityWorkflowOperation.OperationId),
                                  StandardGuid(request.OperationId))
                              & Builders<BsonDocument>.Filter.Eq(
                                  nameof(FinishedGoodIdentityWorkflowOperation.OperationFingerprint),
                                  request.OperationFingerprint)
                              & Builders<BsonDocument>.Filter.Eq(
                                  nameof(FinishedGoodIdentityWorkflowOperation.TemporalStorageVersion),
                                  FinishedGoodIdentityWorkflowOperation.CurrentTemporalStorageVersion)
                              & Builders<BsonDocument>.Filter.Eq(
                                  nameof(FinishedGoodIdentityWorkflowOperation.Version), before.Version + 1)
                              & Builders<BsonDocument>.Filter.Eq(
                                  nameof(FinishedGoodIdentityWorkflowOperation.LeaseOwner), request.LeaseOwner)
                              & Builders<BsonDocument>.Filter.Eq(
                                  nameof(FinishedGoodIdentityWorkflowOperation.LeaseGeneration),
                                  before.LeaseGeneration + 1)
                              & Builders<BsonDocument>.Filter.Eq(
                                  nameof(FinishedGoodIdentityWorkflowOperation.AdmissionScopeSnapshot),
                                  beforeDocument[nameof(FinishedGoodIdentityWorkflowOperation.AdmissionScopeSnapshot)])
                              & Builders<BsonDocument>.Filter.Eq(
                                  MutationEvaluatedAtUtcField,
                                  claimedWithoutLease[MutationEvaluatedAtUtcField])
                              & ProjectedDocumentFitsFilter(ProjectedRoot(claimFields));
            var claimedWithMarker = await _documents.FindOneAndUpdateAsync(
                session,
                leaseFilter,
                new BsonDocumentUpdateDefinition<BsonDocument>(new BsonDocument
                {
                    { "$set", new BsonDocument(
                        nameof(FinishedGoodIdentityWorkflowOperation.LeaseUntilUtcTicksV1), leaseUntilUtcTicks) },
                    { "$currentDate", new BsonDocument(MutationEvaluatedAtUtcField, true) }
                }),
                ReturnAfterOptions,
                cancellationToken);
            if (claimedWithMarker is null)
            {
                await session.AbortTransactionAsync(CancellationToken.None);
                return null;
            }

            DeserializeAndValidate(claimedWithMarker);
            var claimEvaluatedAtUtcTicks = ServerDateTicks(claimedWithMarker);
            if (leaseUntilUtcTicks <= claimEvaluatedAtUtcTicks)
            {
                await session.AbortTransactionAsync(CancellationToken.None);
                return null;
            }

            var claimedDocument = await RemoveEvaluationMarkerAsync(
                session,
                claimedWithMarker,
                claimEvaluatedAtUtcTicks,
                cancellationToken);
            if (claimedDocument is null)
            {
                await session.AbortTransactionAsync(CancellationToken.None);
                return null;
            }

            var claimed = DeserializeAndValidate(claimedDocument);
            await session.CommitTransactionAsync(cancellationToken);
            return new FinishedGoodIdentityWorkflowClaim(
                _tenantId,
                claimed.OperationId,
                claimed.FinishedGoodId,
                claimed.GskuId,
                claimed.ProductDefinitionRevisionId,
                claimed.OperationFingerprint,
                request.LeaseOwner,
                claimed.LeaseGeneration,
                claimed.Checkpoint,
                claimed.LeaseUntilUtcTicksV1!.Value);
        }
        catch
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(CancellationToken.None);
            }

            throw;
        }
    }

    private async Task<FinishedGoodIdentityWorkflowAdvanceResult> AdvanceAtPhysicalServerTimeAsync(
        FinishedGoodIdentityWorkflowClaim claim,
        FinishedGoodIdentityWorkflowCheckpointMutation mutation,
        FilterDefinition<BsonDocument> physicalFilter,
        BsonDocument beforeDocument,
        FinishedGoodIdentityWorkflowOperation before,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumPhysicalMutationTransactionAttempts; attempt++)
        {
            try
            {
                return await AdvanceTransactionAttemptAsync(
                    claim,
                    mutation,
                    physicalFilter,
                    beforeDocument,
                    before,
                    cancellationToken);
            }
            catch (MongoException exception) when (
                IsRetryablePhysicalMutationTransaction(exception) && !cancellationToken.IsCancellationRequested)
            {
                if (attempt == MaximumPhysicalMutationTransactionAttempts)
                {
                    return new(false, null, "FINISHED_GOOD_IDENTITY_WORKFLOW_STALE_CLAIM");
                }

                await Task.Delay(PhysicalMutationRetryDelay(attempt), cancellationToken);
            }
        }

        return new(false, null, "FINISHED_GOOD_IDENTITY_WORKFLOW_STALE_CLAIM");
    }

    private async Task<FinishedGoodIdentityWorkflowAdvanceResult> AdvanceTransactionAttemptAsync(
        FinishedGoodIdentityWorkflowClaim claim,
        FinishedGoodIdentityWorkflowCheckpointMutation mutation,
        FilterDefinition<BsonDocument> physicalFilter,
        BsonDocument beforeDocument,
        FinishedGoodIdentityWorkflowOperation before,
        CancellationToken cancellationToken)
    {
        var changedFields = new BsonDocument
        {
            { nameof(FinishedGoodIdentityWorkflowOperation.Checkpoint), (int)mutation.NextCheckpoint },
            { nameof(FinishedGoodIdentityWorkflowOperation.RecoveryDisposition), (int)mutation.RecoveryDisposition },
            { nameof(FinishedGoodIdentityWorkflowOperation.NextAttemptAtUtcTicksV1),
                mutation.NextAttemptAtUtcTicksV1.HasValue
                    ? new BsonInt64(mutation.NextAttemptAtUtcTicksV1.Value)
                    : BsonNull.Value },
            { nameof(FinishedGoodIdentityWorkflowOperation.LastFailureCode),
                mutation.LastFailureCode is null ? BsonNull.Value : new BsonString(mutation.LastFailureCode) },
            { nameof(FinishedGoodIdentityWorkflowOperation.UpdatedAtUtcTicksV1), before.UpdatedAtUtcTicksV1 },
            { nameof(FinishedGoodIdentityWorkflowOperation.UpdatedAt),
                beforeDocument[nameof(FinishedGoodIdentityWorkflowOperation.UpdatedAt)] },
            { nameof(FinishedGoodIdentityWorkflowOperation.Version), before.Version + 1 },
            { MutationEvaluatedAtUtcField, new BsonDateTime(0) }
        };
        if (mutation.ReleaseLease)
        {
            changedFields[nameof(FinishedGoodIdentityWorkflowOperation.LeaseOwner)] = BsonNull.Value;
            changedFields[nameof(FinishedGoodIdentityWorkflowOperation.LeaseUntilUtcTicksV1)] = BsonNull.Value;
        }

        physicalFilter &= Builders<BsonDocument>.Filter.Eq(
                              nameof(FinishedGoodIdentityWorkflowOperation.Version), before.Version)
                          & Builders<BsonDocument>.Filter.Eq(
                              nameof(FinishedGoodIdentityWorkflowOperation.AdmissionScopeSnapshot),
                              beforeDocument[nameof(FinishedGoodIdentityWorkflowOperation.AdmissionScopeSnapshot)])
                          & ProjectedDocumentFitsFilter(ProjectedRoot(changedFields));

        using var session = await _documents.Database.Client.StartSessionAsync(
            cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            var setFields = new BsonDocument
            {
                { nameof(FinishedGoodIdentityWorkflowOperation.Checkpoint), (int)mutation.NextCheckpoint },
                { nameof(FinishedGoodIdentityWorkflowOperation.RecoveryDisposition), (int)mutation.RecoveryDisposition },
                { nameof(FinishedGoodIdentityWorkflowOperation.NextAttemptAtUtcTicksV1),
                    mutation.NextAttemptAtUtcTicksV1.HasValue
                        ? new BsonInt64(mutation.NextAttemptAtUtcTicksV1.Value)
                        : BsonNull.Value },
                { nameof(FinishedGoodIdentityWorkflowOperation.LastFailureCode),
                    mutation.LastFailureCode is null ? BsonNull.Value : new BsonString(mutation.LastFailureCode) }
            };
            if (mutation.ReleaseLease)
            {
                setFields[nameof(FinishedGoodIdentityWorkflowOperation.LeaseOwner)] = BsonNull.Value;
                setFields[nameof(FinishedGoodIdentityWorkflowOperation.LeaseUntilUtcTicksV1)] = BsonNull.Value;
            }

            var updatedWithMarker = await _documents.FindOneAndUpdateAsync(
                session,
                physicalFilter,
                new BsonDocumentUpdateDefinition<BsonDocument>(new BsonDocument
                {
                    { "$set", setFields },
                    { "$inc", new BsonDocument(nameof(FinishedGoodIdentityWorkflowOperation.Version), 1) },
                    { "$currentDate", new BsonDocument(MutationEvaluatedAtUtcField, true) }
                }),
                ReturnAfterOptions,
                cancellationToken);
            if (updatedWithMarker is null)
            {
                await session.AbortTransactionAsync(CancellationToken.None);
                return new(false, null, "FINISHED_GOOD_IDENTITY_WORKFLOW_STALE_CLAIM");
            }

            DeserializeAndValidate(updatedWithMarker);
            var mutationEvaluatedAtUtcTicks = ServerDateTicks(updatedWithMarker);
            if (claim.LeaseUntilUtcTicksV1 <= mutationEvaluatedAtUtcTicks)
            {
                await session.AbortTransactionAsync(CancellationToken.None);
                return new(false, null, "FINISHED_GOOD_IDENTITY_WORKFLOW_STALE_CLAIM");
            }

            var updatedDocument = await RemoveEvaluationMarkerAsync(
                session,
                updatedWithMarker,
                mutationEvaluatedAtUtcTicks,
                cancellationToken);
            if (updatedDocument is null)
            {
                await session.AbortTransactionAsync(CancellationToken.None);
                return new(false, null, "FINISHED_GOOD_IDENTITY_WORKFLOW_STALE_CLAIM");
            }

            var updated = DeserializeAndValidate(updatedDocument);
            await session.CommitTransactionAsync(cancellationToken);
            return new(true, updated, null);
        }
        catch
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(CancellationToken.None);
            }

            throw;
        }
    }

    private async Task<BsonDocument?> RemoveEvaluationMarkerAsync(
        IClientSessionHandle session,
        BsonDocument operationWithMarker,
        long evaluatedAtUtcTicks,
        CancellationToken cancellationToken)
    {
        var cleanupFilter = Builders<BsonDocument>.Filter.Eq(
                                "_id", operationWithMarker["_id"])
                            & Builders<BsonDocument>.Filter.Eq(
                                nameof(FinishedGoodIdentityWorkflowOperation.Version),
                                operationWithMarker[nameof(FinishedGoodIdentityWorkflowOperation.Version)])
                            & Builders<BsonDocument>.Filter.Eq(
                                MutationEvaluatedAtUtcField,
                                operationWithMarker[MutationEvaluatedAtUtcField]);
        return await _documents.FindOneAndUpdateAsync(
            session,
            cleanupFilter,
            new BsonDocumentUpdateDefinition<BsonDocument>(new BsonDocument
            {
                { "$set", new BsonDocument
                {
                    { nameof(FinishedGoodIdentityWorkflowOperation.UpdatedAtUtcTicksV1), evaluatedAtUtcTicks },
                    { nameof(FinishedGoodIdentityWorkflowOperation.UpdatedAt), DateTimeOffsetArray(evaluatedAtUtcTicks) }
                } },
                { "$unset", new BsonDocument(MutationEvaluatedAtUtcField, string.Empty) }
            }),
            ReturnAfterOptions,
            cancellationToken);
    }

    private FilterDefinition<BsonDocument> ActiveTenantDocumentFilter =>
        Builders<BsonDocument>.Filter.Eq(
            nameof(FinishedGoodIdentityWorkflowOperation.TenantId), StandardGuid(_tenantId))
        & Builders<BsonDocument>.Filter.Eq(nameof(FinishedGoodIdentityWorkflowOperation.IsDeleted), false);

    private FilterDefinition<BsonDocument> TenantIdentityDocumentFilter(
        FinishedGoodIdentityWorkflowOperation operation) =>
        ActiveTenantDocumentFilter
        & Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq(
                nameof(FinishedGoodIdentityWorkflowOperation.OperationId), StandardGuid(operation.OperationId)),
            Builders<BsonDocument>.Filter.Eq(
                nameof(FinishedGoodIdentityWorkflowOperation.StartIdempotencyKey), operation.StartIdempotencyKey),
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq(
                    nameof(FinishedGoodIdentityWorkflowOperation.FinishedGoodId),
                    StandardGuid(operation.FinishedGoodId)),
                Builders<BsonDocument>.Filter.Eq(
                    nameof(FinishedGoodIdentityWorkflowOperation.ExpectedFinishedGoodVersion),
                    operation.ExpectedFinishedGoodVersion)));

    private async Task<FinishedGoodIdentityWorkflowReserveResult> ReadDuplicateReservationAsync(
        FinishedGoodIdentityWorkflowOperation operation,
        CancellationToken cancellationToken)
    {
        try
        {
            var existing = await FindReservationIdentityAsync(operation, cancellationToken);
            return existing is not null && IsExactReplay(existing, operation)
                ? new(true, true, existing, null)
                : new(false, false, null, "FINISHED_GOOD_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT");
        }
        catch (InvalidOperationException)
        {
            return new(false, false, null, "FINISHED_GOOD_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT");
        }
    }

    private async Task<FinishedGoodIdentityWorkflowOperation?> FindReservationIdentityAsync(
        FinishedGoodIdentityWorkflowOperation operation,
        CancellationToken cancellationToken)
    {
        var persisted = await _documents.Find(TenantIdentityDocumentFilter(operation))
            .FirstOrDefaultAsync(cancellationToken);
        return persisted is null ? null : DeserializeAndValidate(persisted);
    }

    private FilterDefinition<BsonDocument> BuildClaimDocumentFilter(
        FinishedGoodIdentityWorkflowClaimRequest request) =>
        ActiveTenantDocumentFilter
        & Builders<BsonDocument>.Filter.Eq(
            nameof(FinishedGoodIdentityWorkflowOperation.OperationId), StandardGuid(request.OperationId))
        & Builders<BsonDocument>.Filter.Eq(
            nameof(FinishedGoodIdentityWorkflowOperation.OperationFingerprint), request.OperationFingerprint)
        & Builders<BsonDocument>.Filter.Eq(
            nameof(FinishedGoodIdentityWorkflowOperation.TemporalStorageVersion),
            FinishedGoodIdentityWorkflowOperation.CurrentTemporalStorageVersion)
        & new BsonDocumentFilterDefinition<BsonDocument>(new BsonDocument(
            nameof(FinishedGoodIdentityWorkflowOperation.Checkpoint),
            new BsonDocument("$in", new BsonArray(request.EligibleCheckpoints.Select(
                checkpoint => new BsonInt32((int)checkpoint))))))
        & Builders<BsonDocument>.Filter.Eq(
            nameof(FinishedGoodIdentityWorkflowOperation.LeaseGeneration), request.ExpectedLeaseGeneration)
        & ServerEvaluatedRecoveryEligibilityFilter();

    private FilterDefinition<BsonDocument> BuildClaimDocumentFilterAt(
        FinishedGoodIdentityWorkflowClaimRequest request,
        long serverUtcTicks) =>
        ActiveTenantDocumentFilter
        & Builders<BsonDocument>.Filter.Eq(
            nameof(FinishedGoodIdentityWorkflowOperation.OperationId), StandardGuid(request.OperationId))
        & Builders<BsonDocument>.Filter.Eq(
            nameof(FinishedGoodIdentityWorkflowOperation.OperationFingerprint), request.OperationFingerprint)
        & Builders<BsonDocument>.Filter.Eq(
            nameof(FinishedGoodIdentityWorkflowOperation.TemporalStorageVersion),
            FinishedGoodIdentityWorkflowOperation.CurrentTemporalStorageVersion)
        & new BsonDocumentFilterDefinition<BsonDocument>(new BsonDocument(
            nameof(FinishedGoodIdentityWorkflowOperation.Checkpoint),
            new BsonDocument("$in", new BsonArray(request.EligibleCheckpoints.Select(
                checkpoint => new BsonInt32((int)checkpoint))))))
        & Builders<BsonDocument>.Filter.Eq(
            nameof(FinishedGoodIdentityWorkflowOperation.LeaseGeneration), request.ExpectedLeaseGeneration)
        & Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq(
                nameof(FinishedGoodIdentityWorkflowOperation.NextAttemptAtUtcTicksV1), BsonNull.Value),
            Builders<BsonDocument>.Filter.Lte(
                nameof(FinishedGoodIdentityWorkflowOperation.NextAttemptAtUtcTicksV1), serverUtcTicks))
        & Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq(
                    nameof(FinishedGoodIdentityWorkflowOperation.LeaseOwner), BsonNull.Value),
                Builders<BsonDocument>.Filter.Eq(
                    nameof(FinishedGoodIdentityWorkflowOperation.LeaseUntilUtcTicksV1), BsonNull.Value)),
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Type(
                    nameof(FinishedGoodIdentityWorkflowOperation.LeaseOwner), BsonType.String),
                Builders<BsonDocument>.Filter.Type(
                    nameof(FinishedGoodIdentityWorkflowOperation.LeaseUntilUtcTicksV1), BsonType.Int64),
                Builders<BsonDocument>.Filter.Lte(
                    nameof(FinishedGoodIdentityWorkflowOperation.LeaseUntilUtcTicksV1), serverUtcTicks)));

    private FilterDefinition<BsonDocument> BuildDiscoverableDocumentFilter() =>
        ActiveTenantDocumentFilter
        & Builders<BsonDocument>.Filter.Eq(
            nameof(FinishedGoodIdentityWorkflowOperation.TemporalStorageVersion),
            FinishedGoodIdentityWorkflowOperation.CurrentTemporalStorageVersion)
        & new BsonDocumentFilterDefinition<BsonDocument>(new BsonDocument(
            nameof(FinishedGoodIdentityWorkflowOperation.Checkpoint),
            new BsonDocument("$nin", new BsonArray(TerminalCheckpoints.Select(
                checkpoint => new BsonInt32((int)checkpoint))))))
        & ServerEvaluatedRecoveryEligibilityFilter();

    private FilterDefinition<BsonDocument> BuildAdvanceDocumentFilter(
        FinishedGoodIdentityWorkflowClaim claim) =>
        ActiveTenantDocumentFilter
        & Builders<BsonDocument>.Filter.Eq(
            nameof(FinishedGoodIdentityWorkflowOperation.OperationId), StandardGuid(claim.OperationId))
        & Builders<BsonDocument>.Filter.Eq(
            nameof(FinishedGoodIdentityWorkflowOperation.OperationFingerprint), claim.OperationFingerprint)
        & Builders<BsonDocument>.Filter.Eq(
            nameof(FinishedGoodIdentityWorkflowOperation.TemporalStorageVersion),
            FinishedGoodIdentityWorkflowOperation.CurrentTemporalStorageVersion)
        & Builders<BsonDocument>.Filter.Eq(
            nameof(FinishedGoodIdentityWorkflowOperation.Checkpoint), (int)claim.Checkpoint)
        & Builders<BsonDocument>.Filter.Eq(
            nameof(FinishedGoodIdentityWorkflowOperation.LeaseOwner), claim.LeaseOwner)
        & Builders<BsonDocument>.Filter.Eq(
            nameof(FinishedGoodIdentityWorkflowOperation.LeaseGeneration), claim.LeaseGeneration)
        & new BsonDocumentFilterDefinition<BsonDocument>(new BsonDocument(
            "$expr",
            new BsonDocument("$and", new BsonArray
            {
                IsInt64Expression("$" + nameof(FinishedGoodIdentityWorkflowOperation.LeaseUntilUtcTicksV1)),
                new BsonDocument("$gt", new BsonArray
                {
                    "$" + nameof(FinishedGoodIdentityWorkflowOperation.LeaseUntilUtcTicksV1),
                    MongoServerUtcTicksExpression()
                })
            })));

    internal static FilterDefinition<BsonDocument> ServerEvaluatedRecoveryEligibilityFilter()
    {
        var serverUtcTicks = MongoServerUtcTicksExpression();
        var nextAttempt = "$" + nameof(FinishedGoodIdentityWorkflowOperation.NextAttemptAtUtcTicksV1);
        var leaseOwner = "$" + nameof(FinishedGoodIdentityWorkflowOperation.LeaseOwner);
        var leaseUntil = "$" + nameof(FinishedGoodIdentityWorkflowOperation.LeaseUntilUtcTicksV1);
        return new BsonDocumentFilterDefinition<BsonDocument>(new BsonDocument(
            "$expr",
            new BsonDocument("$and", new BsonArray
            {
                new BsonDocument("$or", new BsonArray
                {
                    new BsonDocument("$eq", new BsonArray
                    {
                        new BsonDocument("$ifNull", new BsonArray { nextAttempt, BsonNull.Value }),
                        BsonNull.Value
                    }),
                    new BsonDocument("$and", new BsonArray
                    {
                        IsInt64Expression(nextAttempt),
                        new BsonDocument("$lte", new BsonArray { nextAttempt, serverUtcTicks })
                    })
                }),
                new BsonDocument("$or", new BsonArray
                {
                    new BsonDocument("$and", new BsonArray
                    {
                        new BsonDocument("$eq", new BsonArray
                        {
                            new BsonDocument("$ifNull", new BsonArray { leaseOwner, BsonNull.Value }),
                            BsonNull.Value
                        }),
                        new BsonDocument("$eq", new BsonArray
                        {
                            new BsonDocument("$ifNull", new BsonArray { leaseUntil, BsonNull.Value }),
                            BsonNull.Value
                        })
                    }),
                    new BsonDocument("$and", new BsonArray
                    {
                        new BsonDocument("$eq", new BsonArray
                        {
                            new BsonDocument("$type", leaseOwner),
                            "string"
                        }),
                        IsInt64Expression(leaseUntil),
                        new BsonDocument("$lte", new BsonArray { leaseUntil, serverUtcTicks })
                    })
                })
            })));
    }

    private static readonly FinishedGoodIdentityWorkflowCheckpoint[] TerminalCheckpoints =
    [
        FinishedGoodIdentityWorkflowCheckpoint.Completed,
        FinishedGoodIdentityWorkflowCheckpoint.AwaitingMakerReplay,
        FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired,
        FinishedGoodIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
        FinishedGoodIdentityWorkflowCheckpoint.Superseded
    ];

    private static bool IsTerminalCheckpoint(FinishedGoodIdentityWorkflowCheckpoint checkpoint) =>
        TerminalCheckpoints.Contains(checkpoint);

    private bool IsValidReservation(FinishedGoodIdentityWorkflowReservation reservation)
    {
        try
        {
            reservation.AdmissionScopeSnapshot.EnsureValid();
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return _tenantId != Guid.Empty
            && reservation.OperationId != Guid.Empty
            && reservation.FinishedGoodId != Guid.Empty
            && reservation.GskuId != Guid.Empty
            && reservation.ProductDefinitionRevisionId != Guid.Empty
            && reservation.MakerSubjectId != Guid.Empty
            && reservation.ExpectedFinishedGoodVersion >= 0
            && IsExactBounded(reservation.StartIdempotencyKey, 256)
            && IsLowerHex(reservation.OperationFingerprint, 64)
            && reservation.DueAtUtcTicksV1 is null or > 0
            && reservation.AdmissionScopeSnapshot.TenantId == _tenantId
            && reservation.AdmissionScopeSnapshot.FinishedGoodId == reservation.FinishedGoodId
            && reservation.AdmissionScopeSnapshot.GskuId == reservation.GskuId
            && reservation.AdmissionScopeSnapshot.ProductDefinitionRevisionId == reservation.ProductDefinitionRevisionId
            && reservation.AdmissionScopeSnapshot.AdmissionActorSubjectId == reservation.MakerSubjectId;
    }

    private static bool IsExactReplay(
        FinishedGoodIdentityWorkflowOperation existing,
        FinishedGoodIdentityWorkflowOperation candidate) =>
        existing.TenantId == candidate.TenantId
        && existing.OperationId == candidate.OperationId
        && existing.FinishedGoodId == candidate.FinishedGoodId
        && existing.GskuId == candidate.GskuId
        && existing.ProductDefinitionRevisionId == candidate.ProductDefinitionRevisionId
        && existing.MakerSubjectId == candidate.MakerSubjectId
        && existing.ExpectedFinishedGoodVersion == candidate.ExpectedFinishedGoodVersion
        && existing.StartIdempotencyKey == candidate.StartIdempotencyKey
        && existing.OperationFingerprint == candidate.OperationFingerprint
        && existing.DueAtUtcTicksV1 == candidate.DueAtUtcTicksV1
        && AdmissionSnapshotsEqual(existing.AdmissionScopeSnapshot, candidate.AdmissionScopeSnapshot);

    private static bool AdmissionSnapshotsEqual(
        FinishedGoodLifecycleAdmissionScopeSnapshot left,
        FinishedGoodLifecycleAdmissionScopeSnapshot right) =>
        left.SnapshotVersion == right.SnapshotVersion
        && left.TenantId == right.TenantId
        && left.FinishedGoodId == right.FinishedGoodId
        && left.GskuId == right.GskuId
        && left.ProductDefinitionRevisionId == right.ProductDefinitionRevisionId
        && left.AdmissionCommandId == right.AdmissionCommandId
        && left.AdmissionActorSubjectId == right.AdmissionActorSubjectId
        && left.AdmissionObservedAtUtcTicksV1 == right.AdmissionObservedAtUtcTicksV1
        && left.ScopePolicyId == right.ScopePolicyId
        && left.ScopePolicyVersion == right.ScopePolicyVersion
        && left.ScopeMode == right.ScopeMode
        && left.RolloutStateId == right.RolloutStateId
        && left.RolloutVersion == right.RolloutVersion
        && left.RolloutMode == right.RolloutMode
        && left.IntegrityFingerprint == right.IntegrityFingerprint
        && left.LegalEntityIds.SequenceEqual(right.LegalEntityIds);

    private static bool IsAllowedTransition(
        FinishedGoodIdentityWorkflowCheckpoint current,
        FinishedGoodIdentityWorkflowCheckpoint next) =>
        current == next
        || (current, next) switch
        {
            (FinishedGoodIdentityWorkflowCheckpoint.Prepared,
                FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown
                or FinishedGoodIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart
                or FinishedGoodIdentityWorkflowCheckpoint.Superseded
                or FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                FinishedGoodIdentityWorkflowCheckpoint.WorkflowStarted
                or FinishedGoodIdentityWorkflowCheckpoint.AwaitingMakerReplay
                or FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FinishedGoodIdentityWorkflowCheckpoint.WorkflowStarted,
                FinishedGoodIdentityWorkflowCheckpoint.LocalPendingApplied
                or FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FinishedGoodIdentityWorkflowCheckpoint.LocalPendingApplied,
                FinishedGoodIdentityWorkflowCheckpoint.AwaitingDecision
                or FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FinishedGoodIdentityWorkflowCheckpoint.AwaitingDecision,
                FinishedGoodIdentityWorkflowCheckpoint.DecisionObserved
                or FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FinishedGoodIdentityWorkflowCheckpoint.DecisionObserved,
                FinishedGoodIdentityWorkflowCheckpoint.ApprovalValidated
                or FinishedGoodIdentityWorkflowCheckpoint.DecisionApplied
                or FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FinishedGoodIdentityWorkflowCheckpoint.ApprovalValidated,
                FinishedGoodIdentityWorkflowCheckpoint.DecisionApplied
                or FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FinishedGoodIdentityWorkflowCheckpoint.DecisionApplied,
                FinishedGoodIdentityWorkflowCheckpoint.Completed
                or FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            _ => false
        };

    private static bool IsCoherentMutation(
        FinishedGoodIdentityWorkflowCheckpoint current,
        FinishedGoodIdentityWorkflowCheckpointMutation mutation)
    {
        if (!IsOptionalExactBounded(mutation.LastFailureCode, 128)
            || mutation.NextAttemptAtUtcTicksV1 is <= 0)
        {
            return false;
        }

        if (mutation.NextCheckpoint == FinishedGoodIdentityWorkflowCheckpoint.AwaitingDecision)
        {
            return mutation.ReleaseLease
                && mutation.NextAttemptAtUtcTicksV1 is null
                && mutation.LastFailureCode is null
                && mutation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.None;
        }

        if (current == mutation.NextCheckpoint)
        {
            return mutation.ReleaseLease
                && mutation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable
                && mutation.NextAttemptAtUtcTicksV1 is > 0
                && IsExactBounded(mutation.LastFailureCode, 128);
        }

        return mutation.NextAttemptAtUtcTicksV1 is null && mutation.LastFailureCode is null;
    }

    private static bool FitsMongoDocument(FinishedGoodIdentityWorkflowOperation operation) =>
        operation.ToBson().Length <= MaximumBsonDocumentBytes;

    private static BsonDocument ProjectedRoot(BsonDocument changedFields) => new(
        "$mergeObjects",
        new BsonArray { "$$ROOT", changedFields });

    private static FilterDefinition<BsonDocument> ProjectedDocumentFitsFilter(BsonDocument projectedRoot) =>
        new BsonDocumentFilterDefinition<BsonDocument>(new BsonDocument(
            "$expr",
            new BsonDocument("$lte", new BsonArray
            {
                new BsonDocument("$bsonSize", projectedRoot),
                MaximumBsonDocumentBytes
            })));

    private static BsonDocument MongoServerUtcTicksExpression() => new(
        "$add",
        new BsonArray
        {
            new BsonInt64(DotNetUnixEpochUtcTicks),
            new BsonDocument("$multiply", new BsonArray
            {
                new BsonDocument("$toLong", "$$NOW"),
                new BsonInt64(TimeSpan.TicksPerMillisecond)
            })
        });

    private static BsonDocument IsInt64Expression(BsonValue expression) => new(
        "$eq",
        new BsonArray { new BsonDocument("$type", expression), "long" });

    private static BsonBinaryData StandardGuid(Guid value) =>
        new(value, GuidRepresentation.Standard);

    private static readonly FindOneAndUpdateOptions<BsonDocument> ReturnAfterOptions = new()
    {
        IsUpsert = false,
        ReturnDocument = ReturnDocument.After
    };

    private static UpdateDefinition<BsonDocument> CurrentDateUpdate() =>
        new BsonDocumentUpdateDefinition<BsonDocument>(new BsonDocument(
            "$currentDate", new BsonDocument(MutationEvaluatedAtUtcField, true)));

    private static long ServerDateTicks(BsonDocument document)
    {
        if (!document.TryGetValue(MutationEvaluatedAtUtcField, out var value) || !value.IsBsonDateTime)
        {
            throw new InvalidOperationException("FINISHED_GOOD_IDENTITY_WORKFLOW_SERVER_TIME_INVALID");
        }

        return checked(DotNetUnixEpochUtcTicks
            + (value.AsBsonDateTime.MillisecondsSinceEpoch * TimeSpan.TicksPerMillisecond));
    }

    private static BsonArray DateTimeOffsetArray(long utcTicks) => new()
    {
        new BsonInt64(utcTicks),
        0
    };

    private static readonly HashSet<string> KnownOperationElementNames = new(StringComparer.Ordinal)
    {
        "_id",
        nameof(FinishedGoodIdentityWorkflowOperation.TenantId),
        nameof(FinishedGoodIdentityWorkflowOperation.IsDeleted),
        nameof(FinishedGoodIdentityWorkflowOperation.DeletedAt),
        nameof(FinishedGoodIdentityWorkflowOperation.CreatedAt),
        nameof(FinishedGoodIdentityWorkflowOperation.UpdatedAt),
        nameof(FinishedGoodIdentityWorkflowOperation.Version),
        nameof(FinishedGoodIdentityWorkflowOperation.OperationId),
        nameof(FinishedGoodIdentityWorkflowOperation.FinishedGoodId),
        nameof(FinishedGoodIdentityWorkflowOperation.GskuId),
        nameof(FinishedGoodIdentityWorkflowOperation.ProductDefinitionRevisionId),
        nameof(FinishedGoodIdentityWorkflowOperation.MakerSubjectId),
        nameof(FinishedGoodIdentityWorkflowOperation.ExpectedFinishedGoodVersion),
        nameof(FinishedGoodIdentityWorkflowOperation.StartIdempotencyKey),
        nameof(FinishedGoodIdentityWorkflowOperation.OperationFingerprint),
        nameof(FinishedGoodIdentityWorkflowOperation.AdmissionScopeSnapshot),
        nameof(FinishedGoodIdentityWorkflowOperation.Checkpoint),
        nameof(FinishedGoodIdentityWorkflowOperation.RecoveryDisposition),
        nameof(FinishedGoodIdentityWorkflowOperation.DueAtUtcTicksV1),
        nameof(FinishedGoodIdentityWorkflowOperation.NextAttemptAtUtcTicksV1),
        nameof(FinishedGoodIdentityWorkflowOperation.LastFailureCode),
        nameof(FinishedGoodIdentityWorkflowOperation.LeaseOwner),
        nameof(FinishedGoodIdentityWorkflowOperation.LeaseUntilUtcTicksV1),
        nameof(FinishedGoodIdentityWorkflowOperation.LeaseGeneration),
        nameof(FinishedGoodIdentityWorkflowOperation.TemporalStorageVersion),
        nameof(FinishedGoodIdentityWorkflowOperation.CreatedAtUtcTicksV1),
        nameof(FinishedGoodIdentityWorkflowOperation.UpdatedAtUtcTicksV1)
    };

    internal static FinishedGoodIdentityWorkflowOperation DeserializeAndValidate(BsonDocument persisted)
    {
        var knownFields = new BsonDocument();
        foreach (var element in persisted.Elements)
        {
            if (KnownOperationElementNames.Contains(element.Name))
            {
                knownFields.Add(element.Name, element.Value.DeepClone());
            }
        }

        var operation = BsonSerializer.Deserialize<FinishedGoodIdentityWorkflowOperation>(knownFields);
        operation.AdmissionScopeSnapshot.EnsureValid();
        if (operation.TemporalStorageVersion != FinishedGoodIdentityWorkflowOperation.CurrentTemporalStorageVersion
            || operation.TenantId == Guid.Empty
            || operation.OperationId == Guid.Empty
            || operation.FinishedGoodId == Guid.Empty
            || operation.GskuId == Guid.Empty
            || operation.ProductDefinitionRevisionId == Guid.Empty
            || operation.MakerSubjectId == Guid.Empty
            || operation.AdmissionScopeSnapshot.TenantId != operation.TenantId
            || operation.AdmissionScopeSnapshot.FinishedGoodId != operation.FinishedGoodId
            || operation.AdmissionScopeSnapshot.GskuId != operation.GskuId
            || operation.AdmissionScopeSnapshot.ProductDefinitionRevisionId != operation.ProductDefinitionRevisionId
            || operation.AdmissionScopeSnapshot.AdmissionActorSubjectId != operation.MakerSubjectId)
        {
            throw new InvalidOperationException("FINISHED_GOOD_IDENTITY_WORKFLOW_PERSISTED_OPERATION_INVALID");
        }

        return operation;
    }

    private static long UtcNowTicks() => DateTimeOffset.UtcNow.UtcTicks;

    private static bool IsExactBounded(string? value, int maximumLength) =>
        value is { Length: > 0 }
        && value.Length <= maximumLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    private static bool IsOptionalExactBounded(string? value, int maximumLength) =>
        value is null || IsExactBounded(value, maximumLength);

    private static bool IsLowerHex(string? value, int exactLength) =>
        value is not null
        && value.Length == exactLength
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsDriverWrappedUnsatisfiableWriteConcern(MongoWriteException exception) =>
        exception.WriteError is null
        && exception.WriteConcernError is { Code: 100, CodeName: "UnsatisfiableWriteConcern" }
        && exception.InnerException is MongoBulkWriteException<FinishedGoodIdentityWorkflowOperation> bulk
        && bulk.WriteConcernError is { Code: 100, CodeName: "UnsatisfiableWriteConcern" };

    private static bool IsRetryablePhysicalMutationTransaction(MongoException exception) =>
        exception.HasErrorLabel("TransientTransactionError")
        || exception is MongoCommandException { Code: 112, CodeName: "WriteConflict" }
        || exception is MongoWriteException
        {
            WriteError.Code: 112
        };

    private static TimeSpan PhysicalMutationRetryDelay(int failedAttempt) =>
        TimeSpan.FromMilliseconds(InitialPhysicalMutationRetryDelayMilliseconds * (1 << (failedAttempt - 1)));

    private void EnsureIndexes()
    {
        // Bootstrap schema with majority acknowledgement rather than a caller's operation-specific concern.
        // Durable reserve/claim/advance writes continue through _operations unchanged.
        var bootstrapOperations = _operations.WithWriteConcern(WriteConcern.WMajority);

        // All durable identity constraints are non-partial: tombstone and terminal records cannot free a key.
        bootstrapOperations.Indexes.CreateMany(
        [
            new CreateIndexModel<FinishedGoodIdentityWorkflowOperation>(
                Builders<FinishedGoodIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.OperationId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_finished_good_identity_workflow_operation" }),
            new CreateIndexModel<FinishedGoodIdentityWorkflowOperation>(
                Builders<FinishedGoodIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.StartIdempotencyKey),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_finished_good_identity_workflow_start_key" }),
            new CreateIndexModel<FinishedGoodIdentityWorkflowOperation>(
                Builders<FinishedGoodIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.FinishedGoodId)
                    .Ascending(item => item.ExpectedFinishedGoodVersion),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_finished_good_identity_workflow_finished_good_version" }),
            new CreateIndexModel<FinishedGoodIdentityWorkflowOperation>(
                Builders<FinishedGoodIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.IsDeleted)
                    .Ascending(item => item.NextAttemptAtUtcTicksV1).Ascending(item => item.OperationId)
                    .Ascending(item => item.Checkpoint).Ascending(item => item.LeaseUntilUtcTicksV1),
                new CreateIndexOptions { Name = "ix_mdm_finished_good_identity_workflow_recovery" })
        ]);
    }
}
