using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class FirstGskuIdentityWorkflowOperationRepository
    : IFirstGskuIdentityWorkflowOperationRepository
{
    public const string CollectionName = "mdm_first_gsku_identity_workflow_operations";
    private const string RevisionCollectionName = "mdm_product_definition_revisions";
    private const string GskuCollectionName = "mdm_gskus";
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<FirstGskuIdentityWorkflowOperation> _operations;
    private readonly IMongoCollection<ProductDefinitionRevision> _revisions;
    private readonly IMongoCollection<Gsku> _gskus;
    private readonly Guid _tenantId;

    public FirstGskuIdentityWorkflowOperationRepository(
        IMongoDatabase database,
        ITenantContext tenantContext)
    {
        _database = database;
        _operations = database.GetCollection<FirstGskuIdentityWorkflowOperation>(CollectionName);
        _revisions = database.GetCollection<ProductDefinitionRevision>(RevisionCollectionName);
        _gskus = database.GetCollection<Gsku>(GskuCollectionName);
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<FirstGskuIdentityWorkflowReserveResult> ReserveAsync(
        FirstGskuIdentityWorkflowOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!IsValidForReservation(operation))
        {
            return new(false, false, null, "FIRST_GSKU_IDENTITY_WORKFLOW_OPERATION_INVALID");
        }

        operation.TenantId = _tenantId;
        operation.Id = operation.Id == Guid.Empty ? Guid.NewGuid() : operation.Id;
        operation.IsDeleted = false;
        operation.DeletedAt = null;
        operation.Checkpoint = FirstGskuIdentityWorkflowCheckpoint.Prepared;
        operation.RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None;
        operation.WorkflowInstanceId = null;
        operation.WorkflowTemplateVersionId = null;
        operation.ApprovalTaskId = null;
        operation.AssignmentSnapshotId = null;
        operation.StartTransitionLogId = null;
        operation.DecisionTransitionLogId = null;
        operation.DecisionKind = null;
        operation.DecisionObservedAtUtcTicksV1 = null;
        operation.DecisionActorSubjectId = null;
        operation.DecisionReasonCode = null;
        operation.DecisionObjectType = null;
        operation.DecisionObjectId = null;
        operation.DecisionObjectRef = null;
        operation.DecisionWorkflowTemplateId = null;
        operation.DecisionWorkflowTemplateVersionId = null;
        operation.DecisionTaskStatus = null;
        operation.DecisionInstanceStatus = null;
        operation.DecisionTransitionSequence = null;
        operation.DecisionAtUtcTicksV1 = null;
        operation.ApprovalPackApplicabilitySelection = null;
        operation.ApprovalPackUomSelection = null;
        operation.ReferencesValidatedAtUtcTicksV1 = null;
        operation.ApprovalReferenceProofFingerprint = null;
        operation.WithdrawalCommandId = null;
        operation.WithdrawalFingerprint = null;
        operation.WithdrawalRequesterSubjectId = null;
        operation.WithdrawalExpectedGskuVersion = null;
        operation.WithdrawalReasonCode = null;
        operation.WithdrawalComment = null;
        operation.WithdrawalExpectedWorkflowInstanceVersion = null;
        operation.WithdrawalExpectedApprovalTaskVersion = null;
        operation.WithdrawalTransitionLogId = null;
        operation.WithdrawalObservedAtUtcTicksV1 = null;
        operation.WithdrawalTransitionSequence = null;
        operation.WithdrawalResultWorkflowInstanceVersion = null;
        operation.WithdrawalResultApprovalTaskVersion = null;
        operation.WithdrawalTaskStatus = null;
        operation.WithdrawalInstanceStatus = null;
        operation.WithdrawalObjectRef = null;
        operation.WithdrawalRefusalCode = null;
        operation.WithdrawalAwaitingRequesterSinceUtcTicksV1 = null;
        operation.ServiceFaultCount = 0;
        operation.ServiceFaultSinceUtcTicksV1 = null;
        operation.NextAttemptAtUtcTicksV1 = null;
        operation.LastFailureCode = null;
        operation.LeaseOwner = null;
        operation.LeaseUntilUtcTicksV1 = null;
        operation.LeaseGeneration = 0;
        operation.TemporalStorageVersion = FirstGskuIdentityWorkflowOperation.CurrentTemporalStorageVersion;
        operation.Version = 0;
        operation.CreatedAtUtcTicksV1 = operation.CreatedAtUtcTicksV1 > 0
            ? operation.CreatedAtUtcTicksV1
            : DateTimeOffset.UtcNow.UtcTicks;
        operation.UpdatedAtUtcTicksV1 = operation.CreatedAtUtcTicksV1;
        operation.CreatedAt = new DateTimeOffset(operation.CreatedAtUtcTicksV1, TimeSpan.Zero);
        operation.UpdatedAt = operation.CreatedAt;

        // FIX2 item 10: the reservation and a touch of the draft it submits are one transaction. A draft edit reads the
        // reservations and writes the draft in one transaction too, so the two cannot both commit: either the edit
        // sees this submit (and is refused), or the submit finds the draft moved (and is refused) — never an approval
        // started on facts the record no longer has.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
                session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority));
                try
                {
                    await _operations.InsertOneAsync(session, operation, cancellationToken: cancellationToken);
                    var touched = await _gskus.UpdateOneAsync(session,
                        ActiveGskuFilter(operation.GskuId)
                        & Builders<Gsku>.Filter.Eq(item => item.Version, operation.ExpectedGskuVersion)
                        & Builders<Gsku>.Filter.Eq(item => item.LifecycleStatus, ProductIdentityLifecycleStatus.Draft),
                        Builders<Gsku>.Update.Inc(item => item.IdentitySubmitReservations, 1),
                        cancellationToken: cancellationToken);
                    if (touched.MatchedCount != 1
                        && await _gskus.Find(session, ActiveGskuFilter(operation.GskuId)).AnyAsync(cancellationToken))
                    {
                        // The draft moved (an edit, a submit) since it was read: this submit is of facts no longer there.
                        await session.AbortTransactionAsync(cancellationToken);
                        return new(false, false, null, "FIRST_GSKU_IDENTITY_WORKFLOW_DRAFT_CHANGED");
                    }
                    await session.CommitTransactionAsync(cancellationToken);
                }
                catch
                {
                    if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);
                    throw;
                }
                return new(true, false, operation, null);
            }
            catch (MongoException exception) when (exception.HasErrorLabel("TransientTransactionError")
                                                   && exception is not MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey })
            {
                // FIX3 S5: a transient abort is a write conflict with an edit that committed first — or the store itself
                // (a primary stepping down). Read again to say which: a draft that moved is "changed", one that did not is
                // a store that did not answer.
                // FIX4 item 7: — or the SAME submit sent twice, the other one committing first: that one is this one's
                // answer (a replay), not a 503.
                var competing = await CompetingReservationAsync(operation, cancellationToken);
                if (competing is not null)
                    return IsExactReplay(competing, operation)
                        ? new(true, true, competing, null)
                        : new(false, false, null, "FIRST_GSKU_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT");
                if (await DraftMovedAsync(operation, cancellationToken)) return new(false, false, null, "FIRST_GSKU_IDENTITY_WORKFLOW_DRAFT_CHANGED");
                // The other one may not have committed yet (a write conflict is raised before it does): try again, briefly.
                // Once it commits this attempt meets its insert (the replay above); if it aborted, this one goes through.
                if (attempt >= ReserveAttempts) return new(false, false, null, ProductIdentityStore.Unavailable);
                await Task.Delay(TimeSpan.FromMilliseconds(50 << attempt), cancellationToken);
            }
            catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                var existing = await CompetingReservationAsync(operation, cancellationToken);
                return existing is not null && IsExactReplay(existing, operation)
                    ? new(true, true, existing, null)
                    : new(false, false, null, "FIRST_GSKU_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT");
            }
            catch (MongoException exception) when (exception is not MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey })
            {
                // FIX3 S5: no transaction at all (a standalone mongod) or a store that is away — coded, not an uncoded 500.
                return new(false, false, null, ProductIdentityStore.Unavailable);
            }
            catch (NotSupportedException)
            {
                // FIX3 S5: the driver refuses a transaction before the server is asked (a standalone mongod).
                return new(false, false, null, ProductIdentityStore.Unavailable);
            }
        }
    }

    // FIX4 item 7: how often a reservation that met a write conflict with no visible winner tries again (~1.5 s).
    private const int ReserveAttempts = 5;

    // The reservation this one collided with: same operation, same start key, or the same submit of the same pair.
    private async Task<FirstGskuIdentityWorkflowOperation?> CompetingReservationAsync(
        FirstGskuIdentityWorkflowOperation operation, CancellationToken cancellationToken)
    {
        try
        {
            return await _operations.Find(ActiveTenantFilter & Builders<FirstGskuIdentityWorkflowOperation>
                    .Filter.Or(
                        Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.OperationId, operation.OperationId),
                        Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                            item => item.StartIdempotencyKey, operation.StartIdempotencyKey),
                        Builders<FirstGskuIdentityWorkflowOperation>.Filter.And(
                            Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                                item => item.ProductDefinitionRevisionId, operation.ProductDefinitionRevisionId),
                            Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                                item => item.GskuId, operation.GskuId),
                            Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                                item => item.ExpectedRevisionVersion, operation.ExpectedRevisionVersion),
                            Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                                item => item.ExpectedGskuVersion, operation.ExpectedGskuVersion))))
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (MongoException)
        {
            return null;
        }
    }

    private async Task<bool> DraftMovedAsync(FirstGskuIdentityWorkflowOperation operation, CancellationToken cancellationToken)
    {
        try
        {
            var gsku = await _gskus.Find(ActiveGskuFilter(operation.GskuId)).FirstOrDefaultAsync(cancellationToken);
            return gsku is not null && (gsku.Version != operation.ExpectedGskuVersion
                                        || gsku.LifecycleStatus != ProductIdentityLifecycleStatus.Draft);
        }
        catch (MongoException)
        {
            return false;
        }
    }

    public Task<FirstGskuIdentityWorkflowOperation?> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        _operations.Find(ActiveTenantFilter
                         & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                             item => item.OperationId, operationId))
            .FirstOrDefaultAsync(cancellationToken)!;

    public Task<FirstGskuIdentityWorkflowOperation?> GetByStartIdempotencyKeyAsync(
        string startIdempotencyKey,
        CancellationToken cancellationToken = default) =>
        _operations.Find(ActiveTenantFilter
                         & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                             item => item.StartIdempotencyKey, startIdempotencyKey))
            .FirstOrDefaultAsync(cancellationToken)!;

    public async Task<FirstGskuIdentityWorkflowClaim?> TryClaimAsync(
        FirstGskuIdentityWorkflowClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OperationId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.OperationFingerprint)
            || request.EligibleCheckpoints.Count == 0
            || string.IsNullOrWhiteSpace(request.LeaseOwner)
            || request.NowUtcTicks <= 0
            || request.LeaseUntilUtcTicks <= request.NowUtcTicks)
        {
            return null;
        }

        var filter = ActiveTenantFilter
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationId, request.OperationId)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationFingerprint, request.OperationFingerprint)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.TemporalStorageVersion,
                         FirstGskuIdentityWorkflowOperation.CurrentTemporalStorageVersion)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.In(
                         item => item.Checkpoint, request.EligibleCheckpoints)
                     & (request.IgnoreRetrySchedule
                         ? Builders<FirstGskuIdentityWorkflowOperation>.Filter.Empty
                         : Builders<FirstGskuIdentityWorkflowOperation>.Filter.Or(
                             Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.NextAttemptAtUtcTicksV1, null),
                             Builders<FirstGskuIdentityWorkflowOperation>.Filter.Lte(
                                 item => item.NextAttemptAtUtcTicksV1, request.NowUtcTicks)))
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Or(
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, null),
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Lte(
                             item => item.LeaseUntilUtcTicksV1, request.NowUtcTicks));
        var update = Builders<FirstGskuIdentityWorkflowOperation>.Update
            .Set(item => item.LeaseOwner, request.LeaseOwner)
            .Set(item => item.LeaseUntilUtcTicksV1, request.LeaseUntilUtcTicks)
            .Set(item => item.UpdatedAtUtcTicksV1, request.NowUtcTicks)
            .Set(item => item.UpdatedAt, new DateTimeOffset(request.NowUtcTicks, TimeSpan.Zero))
            .Inc(item => item.LeaseGeneration, 1)
            .Inc(item => item.Version, 1);
        var operation = await _operations.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<FirstGskuIdentityWorkflowOperation>
            {
                ReturnDocument = ReturnDocument.After,
                IsUpsert = false
            },
            cancellationToken);
        return operation is null
            ? null
            : new FirstGskuIdentityWorkflowClaim(
                _tenantId,
                operation.OperationId,
                operation.OperationFingerprint,
                request.LeaseOwner,
                operation.LeaseGeneration,
                operation.Checkpoint,
                request.LeaseUntilUtcTicks);
    }

    public async Task<FirstGskuIdentityWorkflowRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks,
        int limit,
        FirstGskuIdentityWorkflowRecoveryCursor? after = null,
        CancellationToken cancellationToken = default)
    {
        if (nowUtcTicks <= 0 || limit is < 1 or > 100 || after?.OperationId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var terminal = new[]
        {
            FirstGskuIdentityWorkflowCheckpoint.Completed,
            FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            FirstGskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
            FirstGskuIdentityWorkflowCheckpoint.Superseded,
            FirstGskuIdentityWorkflowCheckpoint.DecisionNotApplicable
        };
        var filter = ActiveTenantFilter
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.TemporalStorageVersion,
                         FirstGskuIdentityWorkflowOperation.CurrentTemporalStorageVersion)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Nin(item => item.Checkpoint, terminal)
                     // AwaitingMakerReplay is the maker's to continue — EXCEPT a withdrawal an earlier version
                     // parked there: its approval exists at the engine, so the worker must keep reading it.
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Or(
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Ne(
                             item => item.Checkpoint, FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay),
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Ne(
                             item => item.WithdrawalCommandId, null))
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Or(
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.NextAttemptAtUtcTicksV1, null),
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Lte(
                             item => item.NextAttemptAtUtcTicksV1, nowUtcTicks))
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Or(
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, null),
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Lte(
                             item => item.LeaseUntilUtcTicksV1, nowUtcTicks));
        if (after is not null)
        {
            filter &= after.NextAttemptAtUtcTicksV1.HasValue
                ? Builders<FirstGskuIdentityWorkflowOperation>.Filter.Or(
                    Builders<FirstGskuIdentityWorkflowOperation>.Filter.Gt(
                        item => item.NextAttemptAtUtcTicksV1, after.NextAttemptAtUtcTicksV1),
                    Builders<FirstGskuIdentityWorkflowOperation>.Filter.And(
                        Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                            item => item.NextAttemptAtUtcTicksV1, after.NextAttemptAtUtcTicksV1),
                        Builders<FirstGskuIdentityWorkflowOperation>.Filter.Gt(
                            item => item.OperationId, after.OperationId)))
                : Builders<FirstGskuIdentityWorkflowOperation>.Filter.Or(
                    Builders<FirstGskuIdentityWorkflowOperation>.Filter.Ne(
                        item => item.NextAttemptAtUtcTicksV1, null),
                    Builders<FirstGskuIdentityWorkflowOperation>.Filter.And(
                        Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                            item => item.NextAttemptAtUtcTicksV1, null),
                        Builders<FirstGskuIdentityWorkflowOperation>.Filter.Gt(
                            item => item.OperationId, after.OperationId)));
        }

        var rows = await _operations.Find(filter)
            .SortBy(item => item.NextAttemptAtUtcTicksV1)
            .ThenBy(item => item.OperationId)
            .Limit(limit)
            .ToListAsync(cancellationToken);
        var next = rows.Count == limit
            ? new FirstGskuIdentityWorkflowRecoveryCursor(
                rows[^1].NextAttemptAtUtcTicksV1,
                rows[^1].OperationId)
            : null;
        return new(rows, next);
    }

    public async Task<bool> AdvanceAsync(
        FirstGskuIdentityWorkflowClaim claim,
        FirstGskuIdentityWorkflowCheckpointMutation mutation,
        CancellationToken cancellationToken = default)
    {
        if (claim.TenantId != _tenantId
            || mutation.UpdatedAtUtcTicks <= 0
            || !IsAllowedTransition(claim.Checkpoint, mutation.NextCheckpoint)
            || !IsMutationCoherent(claim.Checkpoint, mutation))
        {
            return false;
        }

        var filter = ActiveTenantFilter
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationId, claim.OperationId)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationFingerprint, claim.OperationFingerprint)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.Checkpoint, claim.Checkpoint)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.LeaseOwner, claim.LeaseOwner)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.LeaseGeneration, claim.LeaseGeneration)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Gt(
                         item => item.LeaseUntilUtcTicksV1, mutation.UpdatedAtUtcTicks);
        if (claim.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired
            && mutation.NextCheckpoint == FirstGskuIdentityWorkflowCheckpoint.DecisionObserved)
        {
            filter &= Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.RecoveryDisposition,
                          ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
                      & Builders<FirstGskuIdentityWorkflowOperation>.Filter.In(
                          item => item.LastFailureCode,
                          new[]
                          {
                              "FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED",
                              "REFERENCE_UNAUTHENTICATED"
                          })
                      & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.DecisionKind,
                          ProductIdentityDecisionKind.Approved)
                      & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ApprovalPackApplicabilitySelection, null)
                      & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ApprovalPackUomSelection, null)
                      & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ReferencesValidatedAtUtcTicksV1, null)
                      & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ApprovalReferenceProofFingerprint, null);
        }
        var update = Builders<FirstGskuIdentityWorkflowOperation>.Update
            .Set(item => item.Checkpoint, mutation.NextCheckpoint)
            .Set(item => item.RecoveryDisposition, mutation.RecoveryDisposition)
            .Set(item => item.NextAttemptAtUtcTicksV1, mutation.NextAttemptAtUtcTicksV1)
            .Set(item => item.LastFailureCode, mutation.LastFailureCode)
            .Set(item => item.UpdatedAtUtcTicksV1, mutation.UpdatedAtUtcTicks)
            .Set(item => item.UpdatedAt, new DateTimeOffset(mutation.UpdatedAtUtcTicks, TimeSpan.Zero))
            .Inc(item => item.Version, 1);
        if (mutation.WorkflowInstanceId.HasValue) update = update.Set(item => item.WorkflowInstanceId, mutation.WorkflowInstanceId);
        if (mutation.WorkflowTemplateId.HasValue) update = update.Set(item => item.WorkflowTemplateId, mutation.WorkflowTemplateId);
        if (mutation.WorkflowTemplateVersionId.HasValue) update = update.Set(item => item.WorkflowTemplateVersionId, mutation.WorkflowTemplateVersionId);
        if (mutation.ApprovalTaskId.HasValue) update = update.Set(item => item.ApprovalTaskId, mutation.ApprovalTaskId);
        if (mutation.AssignmentSnapshotId.HasValue) update = update.Set(item => item.AssignmentSnapshotId, mutation.AssignmentSnapshotId);
        if (mutation.StartTransitionLogId.HasValue) update = update.Set(item => item.StartTransitionLogId, mutation.StartTransitionLogId);
        if (mutation.WorkflowStartedAtUtcTicksV1.HasValue)
            update = update.Set(item => item.WorkflowStartedAtUtcTicksV1, mutation.WorkflowStartedAtUtcTicksV1);
        if (mutation.DecisionTransitionLogId.HasValue) update = update.Set(item => item.DecisionTransitionLogId, mutation.DecisionTransitionLogId);
        if (mutation.DecisionKind.HasValue) update = update.Set(item => item.DecisionKind, mutation.DecisionKind);
        if (mutation.DecisionObservedAtUtcTicksV1.HasValue)
            update = update.Set(item => item.DecisionObservedAtUtcTicksV1, mutation.DecisionObservedAtUtcTicksV1);
        if (mutation.DecisionActorSubjectId.HasValue) update = update.Set(item => item.DecisionActorSubjectId, mutation.DecisionActorSubjectId);
        if (mutation.DecisionReasonCode is not null) update = update.Set(item => item.DecisionReasonCode, mutation.DecisionReasonCode);
        if (mutation.DecisionObjectType is not null) update = update.Set(item => item.DecisionObjectType, mutation.DecisionObjectType);
        if (mutation.DecisionObjectId is not null) update = update.Set(item => item.DecisionObjectId, mutation.DecisionObjectId);
        if (mutation.DecisionObjectRef is not null) update = update.Set(item => item.DecisionObjectRef, mutation.DecisionObjectRef);
        if (mutation.DecisionWorkflowTemplateId.HasValue) update = update.Set(item => item.DecisionWorkflowTemplateId, mutation.DecisionWorkflowTemplateId);
        if (mutation.DecisionWorkflowTemplateVersionId.HasValue) update = update.Set(item => item.DecisionWorkflowTemplateVersionId, mutation.DecisionWorkflowTemplateVersionId);
        if (mutation.DecisionTaskStatus is not null) update = update.Set(item => item.DecisionTaskStatus, mutation.DecisionTaskStatus);
        if (mutation.DecisionInstanceStatus is not null) update = update.Set(item => item.DecisionInstanceStatus, mutation.DecisionInstanceStatus);
        if (mutation.DecisionTransitionSequence.HasValue) update = update.Set(item => item.DecisionTransitionSequence, mutation.DecisionTransitionSequence);
        if (mutation.DecisionAtUtcTicksV1.HasValue) update = update.Set(item => item.DecisionAtUtcTicksV1, mutation.DecisionAtUtcTicksV1);
        if (mutation.ApprovalPackApplicabilitySelection is not null) update = update.Set(item => item.ApprovalPackApplicabilitySelection, mutation.ApprovalPackApplicabilitySelection);
        if (mutation.ApprovalPackUomSelection is not null) update = update.Set(item => item.ApprovalPackUomSelection, mutation.ApprovalPackUomSelection);
        if (mutation.ReferencesValidatedAtUtcTicksV1.HasValue) update = update.Set(item => item.ReferencesValidatedAtUtcTicksV1, mutation.ReferencesValidatedAtUtcTicksV1);
        if (mutation.ApprovalReferenceProofFingerprint is not null) update = update.Set(item => item.ApprovalReferenceProofFingerprint, mutation.ApprovalReferenceProofFingerprint);
        if (mutation.WithdrawalCommandId.HasValue) update = update.Set(item => item.WithdrawalCommandId, mutation.WithdrawalCommandId);
        if (mutation.WithdrawalFingerprint is not null) update = update.Set(item => item.WithdrawalFingerprint, mutation.WithdrawalFingerprint);
        if (mutation.WithdrawalRequesterSubjectId.HasValue) update = update.Set(item => item.WithdrawalRequesterSubjectId, mutation.WithdrawalRequesterSubjectId);
        if (mutation.WithdrawalExpectedGskuVersion.HasValue) update = update.Set(item => item.WithdrawalExpectedGskuVersion, mutation.WithdrawalExpectedGskuVersion);
        if (mutation.WithdrawalReasonCode is not null) update = update.Set(item => item.WithdrawalReasonCode, mutation.WithdrawalReasonCode);
        if (mutation.WithdrawalComment is not null) update = update.Set(item => item.WithdrawalComment, mutation.WithdrawalComment);
        if (mutation.WithdrawalExpectedWorkflowInstanceVersion.HasValue) update = update.Set(item => item.WithdrawalExpectedWorkflowInstanceVersion, mutation.WithdrawalExpectedWorkflowInstanceVersion);
        if (mutation.WithdrawalExpectedApprovalTaskVersion.HasValue) update = update.Set(item => item.WithdrawalExpectedApprovalTaskVersion, mutation.WithdrawalExpectedApprovalTaskVersion);
        if (mutation.WithdrawalTransitionLogId.HasValue) update = update.Set(item => item.WithdrawalTransitionLogId, mutation.WithdrawalTransitionLogId);
        if (mutation.WithdrawalObservedAtUtcTicksV1.HasValue) update = update.Set(item => item.WithdrawalObservedAtUtcTicksV1, mutation.WithdrawalObservedAtUtcTicksV1);
        if (mutation.WithdrawalTransitionSequence.HasValue) update = update.Set(item => item.WithdrawalTransitionSequence, mutation.WithdrawalTransitionSequence);
        if (mutation.WithdrawalResultWorkflowInstanceVersion.HasValue) update = update.Set(item => item.WithdrawalResultWorkflowInstanceVersion, mutation.WithdrawalResultWorkflowInstanceVersion);
        if (mutation.WithdrawalResultApprovalTaskVersion.HasValue) update = update.Set(item => item.WithdrawalResultApprovalTaskVersion, mutation.WithdrawalResultApprovalTaskVersion);
        if (mutation.WithdrawalTaskStatus is not null) update = update.Set(item => item.WithdrawalTaskStatus, mutation.WithdrawalTaskStatus);
        if (mutation.WithdrawalInstanceStatus is not null) update = update.Set(item => item.WithdrawalInstanceStatus, mutation.WithdrawalInstanceStatus);
        if (mutation.WithdrawalObjectRef is not null) update = update.Set(item => item.WithdrawalObjectRef, mutation.WithdrawalObjectRef);
        if (mutation.WithdrawalRefusalCode is not null) update = update.Set(item => item.WithdrawalRefusalCode, mutation.WithdrawalRefusalCode);
        if (mutation.WithdrawalAwaitingRequesterSinceUtcTicksV1.HasValue) update = update.Set(item => item.WithdrawalAwaitingRequesterSinceUtcTicksV1, mutation.WithdrawalAwaitingRequesterSinceUtcTicksV1);
        if (mutation.ApprovalAppliedNote is not null) update = update.Set(item => item.ApprovalAppliedNote, mutation.ApprovalAppliedNote);
        if (mutation.ServiceFaultCount.HasValue)
        {
            update = update
                .Set(item => item.ServiceFaultCount, mutation.ServiceFaultCount.Value)
                .Set(item => item.ServiceFaultSinceUtcTicksV1, mutation.ServiceFaultSinceUtcTicksV1);
        }
        if (mutation.ClearWithdrawal)
        {
            update = update
                .Set(item => item.WithdrawalCommandId, null)
                .Set(item => item.WithdrawalFingerprint, null)
                .Set(item => item.WithdrawalRequesterSubjectId, null)
                .Set(item => item.WithdrawalExpectedGskuVersion, null)
                .Set(item => item.WithdrawalReasonCode, null)
                .Set(item => item.WithdrawalComment, null)
                .Set(item => item.WithdrawalExpectedWorkflowInstanceVersion, null)
                .Set(item => item.WithdrawalExpectedApprovalTaskVersion, null)
                .Set(item => item.WithdrawalTransitionLogId, null)
                .Set(item => item.WithdrawalObservedAtUtcTicksV1, null)
                .Set(item => item.WithdrawalTransitionSequence, null)
                .Set(item => item.WithdrawalResultWorkflowInstanceVersion, null)
                .Set(item => item.WithdrawalResultApprovalTaskVersion, null)
                .Set(item => item.WithdrawalTaskStatus, null)
                .Set(item => item.WithdrawalInstanceStatus, null)
                .Set(item => item.WithdrawalObjectRef, null)
                .Set(item => item.WithdrawalRefusalCode, null)
                .Set(item => item.WithdrawalAwaitingRequesterSinceUtcTicksV1, null);
        }
        if (mutation.ReleaseLease)
        {
            update = update.Set(item => item.LeaseOwner, null).Set(item => item.LeaseUntilUtcTicksV1, null);
        }

        var result = await _operations.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public Task ReleaseIfOwnedAsync(
        Guid operationId,
        string leaseOwner,
        long leaseUntilUtcTicks,
        CancellationToken cancellationToken = default) =>
        _operations.UpdateOneAsync(
            ActiveTenantFilter
            & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.OperationId, operationId)
            & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, leaseOwner)
            & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseUntilUtcTicksV1, leaseUntilUtcTicks),
            Builders<FirstGskuIdentityWorkflowOperation>.Update
                .Set(item => item.LeaseOwner, null)
                .Set(item => item.LeaseUntilUtcTicksV1, null),
            cancellationToken: cancellationToken);

    public async Task<FirstGskuIdentitySubmitState> GetSubmitStateAsync(
        Guid gskuId, int currentGskuVersion, CancellationToken cancellationToken = default) =>
        FirstGskuIdentitySubmitState.Of(await LatestForGsku(_operations, ActiveTenantFilter, gskuId)
            .FirstOrDefaultAsync(cancellationToken), currentGskuVersion);

    // The GSKU's latest identity operation — its submits come one per draft version, the newest is the one that counts.
    internal static IFindFluent<FirstGskuIdentityWorkflowOperation, FirstGskuIdentityWorkflowOperation> LatestForGsku(
        IMongoCollection<FirstGskuIdentityWorkflowOperation> operations,
        FilterDefinition<FirstGskuIdentityWorkflowOperation> tenantFilter,
        Guid gskuId,
        IClientSessionHandle? session = null)
    {
        var filter = tenantFilter & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.GskuId, gskuId);
        // FIX3 S10: the latest submit is the one at the highest GSKU version (each submit is of one version, and a
        // version is submitted at most once); creation time only breaks a tie — two writers' clocks may disagree.
        return (session is null ? operations.Find(filter) : operations.Find(session, filter))
            .SortByDescending(item => item.ExpectedGskuVersion)
            .ThenByDescending(item => item.CreatedAtUtcTicksV1)
            .Limit(1);
    }

    public async Task<string?> LatestNotApplicableReasonAsync(Guid gskuId, CancellationToken cancellationToken = default)
    {
        var latest = await _operations.Find(
                ActiveTenantFilter & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.GskuId, gskuId))
            .SortByDescending(item => item.CreatedAtUtcTicksV1)
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);
        return latest?.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.DecisionNotApplicable ? latest.LastFailureCode : null;
    }

    public async Task<string?> LatestApprovalNoteAsync(Guid gskuId, CancellationToken cancellationToken = default)
    {
        var latest = await LatestForGsku(_operations, ActiveTenantFilter, gskuId).FirstOrDefaultAsync(cancellationToken);
        return latest?.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.Completed ? latest.ApprovalAppliedNote : null;
    }

    public const string SelfApprovalReason = "FIRST_GSKU_IDENTITY_SELF_APPROVAL_IGNORED";
    // FIX3 E1: the engine closed the approval without a decision (cancelled by an administrator or by its maker in the
    // Task Center, or expired) — nothing will come, so the pair goes back to draft, as a correction does.
    public const string ClosedWithoutDecisionReason = "FIRST_GSKU_IDENTITY_CLOSED_WITHOUT_DECISION";
    public const string AuditCapacityReached = "AUDIT_INTENT_CAPACITY_REACHED";

    public async Task<FirstGskuIdentityDecisionNotApplicableWriteResult> ApplyDecisionNotApplicableAsync(
        FirstGskuIdentityWorkflowClaim claim,
        FirstGskuIdentityWorkflowOperation operation,
        string reasonCode,
        LocalAuditIntent revisionAuditIntent,
        LocalAuditIntent gskuAuditIntent,
        long updatedAtUtcTicks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(revisionAuditIntent);
        ArgumentNullException.ThrowIfNull(gskuAuditIntent);
        var key = NotApplicableKey(operation);
        if (claim.TenantId != _tenantId
            || claim.OperationId != operation.OperationId
            || claim.OperationFingerprint != operation.OperationFingerprint
            // From DecisionObserved: the engine approved and the approval cannot be applied. From AwaitingDecision
            // (FIX2 item 3): the engine closed the approval with the maker's own decision — nothing else will come.
            || !(claim.Checkpoint is FirstGskuIdentityWorkflowCheckpoint.DecisionObserved
                        // FIX3 E2: the second gate too — the pair is still pending there.
                        or FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated
                    && operation.DecisionKind == ProductIdentityDecisionKind.Approved
                 || claim.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision
                    && reasonCode is SelfApprovalReason or ClosedWithoutDecisionReason)
            || updatedAtUtcTicks <= 0
            || !IsExactBounded(reasonCode, 128)
            || revisionAuditIntent.Operation != ProductAuditOperation.ProductDefinitionRevisionIdentityDecisionNotApplicable
            || gskuAuditIntent.Operation != ProductAuditOperation.GskuIdentityDecisionNotApplicable
            || !ValidNotApplicableAudit(revisionAuditIntent, AuditAggregateType.ProductDefinitionRevision,
                operation.ProductDefinitionRevisionId, operation.ExpectedRevisionVersion + 1, key)
            || !ValidNotApplicableAudit(gskuAuditIntent, AuditAggregateType.Gsku, operation.GskuId,
                operation.ExpectedGskuVersion + 1, key))
        {
            return new(false, false, null, null, "FIRST_GSKU_IDENTITY_NOT_APPLICABLE_CONTRACT_INVALID");
        }

        var currentRevision = await _revisions.Find(ActiveRevisionFilter(operation.ProductDefinitionRevisionId))
            .FirstOrDefaultAsync(cancellationToken);
        var currentGsku = await _gskus.Find(ActiveGskuFilter(operation.GskuId)).FirstOrDefaultAsync(cancellationToken);
        using var session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority));
        try
        {
            var revisionResult = await _revisions.UpdateOneAsync(
                session,
                ActiveRevisionFilter(operation.ProductDefinitionRevisionId)
                & Builders<ProductDefinitionRevision>.Filter.Eq(item => item.Version, operation.ExpectedRevisionVersion + 1)
                & Builders<ProductDefinitionRevision>.Filter.Eq(
                    item => item.LifecycleStatus, ProductIdentityLifecycleStatus.PendingIdentityApproval)
                & ExactBindingFilter<ProductDefinitionRevision>(operation)
                & Builders<ProductDefinitionRevision>.Filter.Where(item => item.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate),
                Builders<ProductDefinitionRevision>.Update
                    .Set(item => item.LifecycleStatus, ProductIdentityLifecycleStatus.Draft)
                    .Set(item => item.UpdatedAt, new DateTimeOffset(updatedAtUtcTicks, TimeSpan.Zero))
                    .Inc(item => item.Version, 1)
                    .Push(item => item.AuditIntents, revisionAuditIntent),
                cancellationToken: cancellationToken);
            var gskuResult = revisionResult.ModifiedCount != 1 ? null : await _gskus.UpdateOneAsync(
                session,
                ActiveGskuFilter(operation.GskuId)
                & Builders<Gsku>.Filter.Eq(item => item.Version, operation.ExpectedGskuVersion + 1)
                & Builders<Gsku>.Filter.Eq(item => item.LifecycleStatus, ProductIdentityLifecycleStatus.PendingIdentityApproval)
                & ExactBindingFilter<Gsku>(operation)
                & Builders<Gsku>.Filter.Where(item => item.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate),
                Builders<Gsku>.Update
                    .Set(item => item.LifecycleStatus, ProductIdentityLifecycleStatus.Draft)
                    .Set(item => item.UpdatedAt, new DateTimeOffset(updatedAtUtcTicks, TimeSpan.Zero))
                    .Inc(item => item.Version, 1)
                    .Push(item => item.AuditIntents, gskuAuditIntent),
                cancellationToken: cancellationToken);
            var operationResult = gskuResult is not { ModifiedCount: 1 } ? null : await _operations.UpdateOneAsync(
                session,
                ClaimFilter(claim, updatedAtUtcTicks),
                Builders<FirstGskuIdentityWorkflowOperation>.Update
                    .Set(item => item.Checkpoint, FirstGskuIdentityWorkflowCheckpoint.DecisionNotApplicable)
                    .Set(item => item.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.None)
                    .Set(item => item.NextAttemptAtUtcTicksV1, null)
                    .Set(item => item.LastFailureCode, reasonCode)
                    .Set(item => item.ServiceFaultCount, 0)
                    .Set(item => item.ServiceFaultSinceUtcTicksV1, null)
                    .Set(item => item.LeaseOwner, null)
                    .Set(item => item.LeaseUntilUtcTicksV1, null)
                    .Set(item => item.UpdatedAtUtcTicksV1, updatedAtUtcTicks)
                    .Set(item => item.UpdatedAt, new DateTimeOffset(updatedAtUtcTicks, TimeSpan.Zero))
                    .Inc(item => item.Version, 1),
                cancellationToken: cancellationToken);
            if (operationResult is not { ModifiedCount: 1 })
            {
                await session.AbortTransactionAsync(cancellationToken);
                // FIX2 item 7: a member at its audit-intent capacity is said as such (the writer waits for delivery),
                // not as a pair conflict (which the processor would quarantine).
                var atCapacity = currentRevision?.AuditIntents.Count >= AuditIntentLimits.MaxPerAggregate
                                 || currentGsku?.AuditIntents.Count >= AuditIntentLimits.MaxPerAggregate;
                return new(false, false, currentRevision, currentGsku, atCapacity
                    ? AuditCapacityReached
                    : "FIRST_GSKU_IDENTITY_NOT_APPLICABLE_PAIR_CONFLICT");
            }
            await session.CommitTransactionAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);
            throw;
        }
        catch (MongoException)
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);
            return new(false, false, currentRevision, currentGsku, "FIRST_GSKU_IDENTITY_NOT_APPLICABLE_PERSISTENCE_UNAVAILABLE");
        }

        currentRevision = await _revisions.Find(ActiveRevisionFilter(operation.ProductDefinitionRevisionId))
            .FirstOrDefaultAsync(cancellationToken);
        currentGsku = await _gskus.Find(ActiveGskuFilter(operation.GskuId)).FirstOrDefaultAsync(cancellationToken);
        return new(true, false, currentRevision, currentGsku, null);
    }

    private static string NotApplicableKey(FirstGskuIdentityWorkflowOperation operation) =>
        FirstGskuIdentityDecisionNotApplicable.Key(operation.OperationId);

    private bool ValidNotApplicableAudit(
        LocalAuditIntent intent,
        AuditAggregateType aggregateType,
        Guid aggregateId,
        int expectedVersion,
        string key) =>
        intent.TenantId == _tenantId
        && intent.AggregateType == aggregateType
        && intent.AggregateId == aggregateId
        && intent.PreVersion == expectedVersion
        && intent.PostVersion == expectedVersion + 1
        && intent.IdempotencyKey == key
        && !string.IsNullOrWhiteSpace(intent.EvidenceHash);

    public async Task<FirstGskuIdentityWithdrawalWriteResult> ApplyWithdrawalAsync(
        FirstGskuIdentityWorkflowClaim claim,
        FirstGskuIdentityWorkflowOperation operation,
        ProductIdentityWorkflowCancellationEvidence cancellationEvidence,
        LocalAuditIntent revisionAuditIntent,
        LocalAuditIntent gskuAuditIntent,
        long updatedAtUtcTicks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(cancellationEvidence);
        ArgumentNullException.ThrowIfNull(revisionAuditIntent);
        ArgumentNullException.ThrowIfNull(gskuAuditIntent);

        if (claim.TenantId != _tenantId
            || claim.OperationId != operation.OperationId
            || claim.OperationFingerprint != operation.OperationFingerprint
            || claim.Checkpoint != FirstGskuIdentityWorkflowCheckpoint.WithdrawalObserved
            || updatedAtUtcTicks <= 0
            || operation.WithdrawalExpectedGskuVersion is not > 0
            || operation.WithdrawalCommandId is not { } commandId
            || commandId == Guid.Empty
            || cancellationEvidence.RequesterSubjectId != operation.MakerSubjectId
            || cancellationEvidence.WorkflowInstanceId != operation.WorkflowInstanceId
            || cancellationEvidence.ApprovalTaskId != operation.ApprovalTaskId
            || cancellationEvidence.IdempotencyKey != commandId.ToString("D")
            || revisionAuditIntent.Operation != ProductAuditOperation.ProductDefinitionRevisionIdentityApprovalWithdrawn
            || gskuAuditIntent.Operation != ProductAuditOperation.GskuIdentityApprovalWithdrawn
            || !ValidWithdrawalAudit(revisionAuditIntent, AuditAggregateType.ProductDefinitionRevision,
                operation.ProductDefinitionRevisionId,
                operation.ExpectedRevisionVersion + 1, commandId, operation.MakerSubjectId)
            || !ValidWithdrawalAudit(gskuAuditIntent, AuditAggregateType.Gsku, operation.GskuId,
                operation.WithdrawalExpectedGskuVersion.Value, commandId, operation.MakerSubjectId))
        {
            return new(false, false, null, null, "FIRST_GSKU_IDENTITY_WITHDRAWAL_CONTRACT_INVALID");
        }

        var currentRevision = await _revisions.Find(
                ActiveRevisionFilter(operation.ProductDefinitionRevisionId))
            .FirstOrDefaultAsync(cancellationToken);
        var currentGsku = await _gskus.Find(ActiveGskuFilter(operation.GskuId))
            .FirstOrDefaultAsync(cancellationToken);
        if (ExactWithdrawalReplay(currentRevision, currentGsku, operation, commandId,
                revisionAuditIntent, gskuAuditIntent))
        {
            return new(true, true, currentRevision, currentGsku, null);
        }

        using var session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction(new TransactionOptions(
            ReadConcern.Snapshot,
            ReadPreference.Primary,
            WriteConcern.WMajority));
        try
        {
            var revisionResult = await _revisions.UpdateOneAsync(
                session,
                ActiveRevisionFilter(operation.ProductDefinitionRevisionId)
                & Builders<ProductDefinitionRevision>.Filter.Eq(
                    item => item.Version, operation.ExpectedRevisionVersion + 1)
                & Builders<ProductDefinitionRevision>.Filter.Eq(
                    item => item.LifecycleStatus, ProductIdentityLifecycleStatus.PendingIdentityApproval)
                & Builders<ProductDefinitionRevision>.Filter.Where(
                    item => item.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate)
                & ExactBindingFilter<ProductDefinitionRevision>(operation),
                Builders<ProductDefinitionRevision>.Update
                    .Set(item => item.LifecycleStatus, ProductIdentityLifecycleStatus.Draft)
                    .Set(item => item.UpdatedAt, new DateTimeOffset(updatedAtUtcTicks, TimeSpan.Zero))
                    .Inc(item => item.Version, 1)
                    .Push(item => item.AuditIntents, revisionAuditIntent),
                cancellationToken: cancellationToken);
            if (revisionResult.ModifiedCount != 1)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(false, false, currentRevision, currentGsku,
                    "FIRST_GSKU_IDENTITY_WITHDRAWAL_PAIR_CONFLICT");
            }

            var gskuResult = await _gskus.UpdateOneAsync(
                session,
                ActiveGskuFilter(operation.GskuId)
                & Builders<Gsku>.Filter.Eq(
                    item => item.Version, operation.WithdrawalExpectedGskuVersion.Value)
                & Builders<Gsku>.Filter.Eq(
                    item => item.LifecycleStatus, ProductIdentityLifecycleStatus.PendingIdentityApproval)
                & Builders<Gsku>.Filter.Where(
                    item => item.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate)
                & ExactBindingFilter<Gsku>(operation),
                Builders<Gsku>.Update
                    .Set(item => item.LifecycleStatus, ProductIdentityLifecycleStatus.Draft)
                    .Set(item => item.UpdatedAt, new DateTimeOffset(updatedAtUtcTicks, TimeSpan.Zero))
                    .Inc(item => item.Version, 1)
                    .Push(item => item.AuditIntents, gskuAuditIntent),
                cancellationToken: cancellationToken);
            if (gskuResult.ModifiedCount != 1)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(false, false, currentRevision, currentGsku,
                    "FIRST_GSKU_IDENTITY_WITHDRAWAL_PAIR_CONFLICT");
            }

            var operationResult = await _operations.UpdateOneAsync(
                session,
                ClaimFilter(claim, updatedAtUtcTicks),
                Builders<FirstGskuIdentityWorkflowOperation>.Update
                    .Set(item => item.Checkpoint, FirstGskuIdentityWorkflowCheckpoint.WithdrawalApplied)
                    .Set(item => item.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.None)
                    .Set(item => item.NextAttemptAtUtcTicksV1, null)
                    .Set(item => item.LastFailureCode, null)
                    .Set(item => item.LeaseOwner, null)
                    .Set(item => item.LeaseUntilUtcTicksV1, null)
                    .Set(item => item.UpdatedAtUtcTicksV1, updatedAtUtcTicks)
                    .Set(item => item.UpdatedAt, new DateTimeOffset(updatedAtUtcTicks, TimeSpan.Zero))
                    .Inc(item => item.Version, 1),
                cancellationToken: cancellationToken);
            if (operationResult.ModifiedCount != 1)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(false, false, currentRevision, currentGsku,
                    "FIRST_GSKU_IDENTITY_WITHDRAWAL_CONCURRENCY_CONFLICT");
            }

            await session.CommitTransactionAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);
            throw;
        }
        catch (MongoException)
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);
            return new(false, false, currentRevision, currentGsku,
                "FIRST_GSKU_IDENTITY_WITHDRAWAL_PERSISTENCE_UNAVAILABLE");
        }

        currentRevision = await _revisions.Find(ActiveRevisionFilter(operation.ProductDefinitionRevisionId))
            .FirstOrDefaultAsync(cancellationToken);
        currentGsku = await _gskus.Find(ActiveGskuFilter(operation.GskuId))
            .FirstOrDefaultAsync(cancellationToken);
        return ExactWithdrawalReplay(currentRevision, currentGsku, operation, commandId,
                revisionAuditIntent, gskuAuditIntent)
            ? new(true, false, currentRevision, currentGsku, null)
            : new(false, false, currentRevision, currentGsku,
                "FIRST_GSKU_IDENTITY_WITHDRAWAL_LOCAL_STATE_INCONSISTENT");
    }

    private FilterDefinition<FirstGskuIdentityWorkflowOperation> ClaimFilter(
        FirstGskuIdentityWorkflowClaim claim,
        long nowUtcTicks) =>
        ActiveTenantFilter
        & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.OperationId, claim.OperationId)
        & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
            item => item.OperationFingerprint, claim.OperationFingerprint)
        & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.Checkpoint, claim.Checkpoint)
        & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, claim.LeaseOwner)
        & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
            item => item.LeaseGeneration, claim.LeaseGeneration)
        & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Gt(item => item.LeaseUntilUtcTicksV1, nowUtcTicks);

    private FilterDefinition<ProductDefinitionRevision> ActiveRevisionFilter(Guid id) =>
        Builders<ProductDefinitionRevision>.Filter.Eq(item => item.TenantId, _tenantId)
        & Builders<ProductDefinitionRevision>.Filter.Eq(item => item.IsDeleted, false)
        & Builders<ProductDefinitionRevision>.Filter.Eq(item => item.Id, id);

    private FilterDefinition<Gsku> ActiveGskuFilter(Guid id) =>
        Builders<Gsku>.Filter.Eq(item => item.TenantId, _tenantId)
        & Builders<Gsku>.Filter.Eq(item => item.IsDeleted, false)
        & Builders<Gsku>.Filter.Eq(item => item.Id, id);

    private static FilterDefinition<T> ExactBindingFilter<T>(FirstGskuIdentityWorkflowOperation operation)
        where T : EntityBase =>
        Builders<T>.Filter.Eq("IdentityWorkflowBinding.WorkflowInstanceId", operation.WorkflowInstanceId)
        & Builders<T>.Filter.Eq("IdentityWorkflowBinding.ApprovalTaskId", operation.ApprovalTaskId)
        & Builders<T>.Filter.Eq("IdentityWorkflowBinding.SubmitterSubjectId", operation.MakerSubjectId);

    private bool ExactWithdrawalReplay(
        ProductDefinitionRevision? revision,
        Gsku? gsku,
        FirstGskuIdentityWorkflowOperation operation,
        Guid commandId,
        LocalAuditIntent revisionAuditIntent,
        LocalAuditIntent gskuAuditIntent) =>
        revision is not null && gsku is not null
        && revision.TenantId == _tenantId && gsku.TenantId == _tenantId
        && revision.LifecycleStatus == ProductIdentityLifecycleStatus.Draft
        && gsku.LifecycleStatus == ProductIdentityLifecycleStatus.Draft
        && revision.Version == operation.ExpectedRevisionVersion + 2
        && gsku.Version == operation.WithdrawalExpectedGskuVersion + 1
        // FIX2 item 7: the intent, or its receipt once delivered and compacted.
        && AuditWriteReadBack.Persisted(revision, revisionAuditIntent, intent =>
            intent.Operation == ProductAuditOperation.ProductDefinitionRevisionIdentityApprovalWithdrawn
            && intent.IdempotencyKey == commandId.ToString("D")
            && intent.EvidenceHash == revisionAuditIntent.EvidenceHash)
        && AuditWriteReadBack.Persisted(gsku, gskuAuditIntent, intent =>
            intent.Operation == ProductAuditOperation.GskuIdentityApprovalWithdrawn
            && intent.IdempotencyKey == commandId.ToString("D")
            && intent.EvidenceHash == gskuAuditIntent.EvidenceHash);

    private bool ValidWithdrawalAudit(
        LocalAuditIntent intent,
        AuditAggregateType aggregateType,
        Guid aggregateId,
        int expectedVersion,
        Guid commandId,
        Guid actorId) =>
        intent.TenantId == _tenantId
        && intent.AggregateType == aggregateType
        && intent.AggregateId == aggregateId
        && intent.PreVersion == expectedVersion
        && intent.PostVersion == expectedVersion + 1
        && intent.ActorId == actorId.ToString("D")
        && intent.IdempotencyKey == commandId.ToString("D")
        && !string.IsNullOrWhiteSpace(intent.EvidenceHash);


    // FIX3 item 1 (B13): this tenant's operations waiting for a person.
    public Task<long> CountManualReconciliationAsync(CancellationToken cancellationToken = default) =>
        _operations.CountDocumentsAsync(ActiveTenantFilter & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(x => x.Checkpoint, FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired),
            cancellationToken: cancellationToken);

    private FilterDefinition<FirstGskuIdentityWorkflowOperation> ActiveTenantFilter =>
        Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.TenantId, _tenantId)
        & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.IsDeleted, false);

    private bool IsValidForReservation(FirstGskuIdentityWorkflowOperation operation) =>
        _tenantId != Guid.Empty
        && operation.OperationId != Guid.Empty
        && operation.ProductDefinitionRevisionId != Guid.Empty
        && operation.GskuId != Guid.Empty
        && operation.GlobalProductId != Guid.Empty
        && IsExactBounded(operation.CreationCommandId, 256)
        && operation.ExpectedRevisionVersion >= 0
        && operation.ExpectedGskuVersion >= 0
        && operation.PackQuantity > 0
        && IsExactBounded(operation.PackApplicabilityCode, 128)
        && IsExactBounded(operation.PackUomCode, 128)
        && IsValidSelection(operation.PackApplicabilitySelection, operation.PackApplicabilityCode)
        && IsValidSelection(operation.PackUomSelection, operation.PackUomCode)
        && operation.MakerSubjectId != Guid.Empty
        && (operation.WorkflowTemplateId.HasValue ^ operation.WorkflowTemplateCode is not null)
        && operation.CandidatePrincipalIds is { Count: >= 1 and <= 100 }
        && operation.CandidatePrincipalIds.All(item => item != Guid.Empty)
        && operation.CandidatePrincipalIds.Distinct().Count() == operation.CandidatePrincipalIds.Count
        && IsExactBounded(operation.ReasonCode, 128)
        && operation.ObjectType == "gsku"
        && operation.ObjectId == operation.GskuId.ToString("D")
        && IsExactBounded(operation.ObjectRef, 256)
        && IsExactBounded(operation.StartIdempotencyKey, 256)
        && IsExactBounded(operation.OperationFingerprint, 256)
        && (!operation.WorkflowTemplateId.HasValue || operation.WorkflowTemplateId != Guid.Empty)
        && (operation.WorkflowTemplateCode is null || IsExactBounded(operation.WorkflowTemplateCode, 128))
        && operation.TemporalStorageVersion is 0 or FirstGskuIdentityWorkflowOperation.CurrentTemporalStorageVersion;

    private static bool IsValidSelection(ReferenceCatalogSelection selection, string expectedCode) =>
        selection is not null
        && IsExactBounded(selection.SetCode, 128)
        && selection.ValueCode == expectedCode
        && selection.CatalogVersionId != Guid.Empty
        && selection.CatalogVersionNumber > 0
        && selection.ResolvedAtUtc.Offset == TimeSpan.Zero;

    private static bool IsExactBounded(string? value, int maximumLength) =>
        value is { Length: > 0 }
        && value.Length <= maximumLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    private static bool IsAllowedTransition(
        FirstGskuIdentityWorkflowCheckpoint current,
        FirstGskuIdentityWorkflowCheckpoint next) =>
        current == next
        || (current, next) switch
        {
            (FirstGskuIdentityWorkflowCheckpoint.Prepared, FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown) => true,
            (FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted
                or FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired
                // A maker's resubmit whose outcome is unknown again goes back to the worker's lookup.
                or FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown) => true,
            (FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted,
                FirstGskuIdentityWorkflowCheckpoint.RevisionPendingApplied
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.RevisionPendingApplied,
                FirstGskuIdentityWorkflowCheckpoint.PairPendingApplied
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.PairPendingApplied,
                FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision,
                FirstGskuIdentityWorkflowCheckpoint.DecisionObserved
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.DecisionObserved,
                FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated
                or FirstGskuIdentityWorkflowCheckpoint.GskuDraftRestored
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated,
                FirstGskuIdentityWorkflowCheckpoint.RevisionApproved
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.RevisionApproved,
                FirstGskuIdentityWorkflowCheckpoint.PairApproved
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.PairApproved,
                FirstGskuIdentityWorkflowCheckpoint.Completed
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.GskuDraftRestored,
                FirstGskuIdentityWorkflowCheckpoint.PairDraftRestored
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.PairDraftRestored,
                FirstGskuIdentityWorkflowCheckpoint.Completed
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision,
                FirstGskuIdentityWorkflowCheckpoint.WithdrawalRequested) => true,
            (FirstGskuIdentityWorkflowCheckpoint.WithdrawalRequested,
                FirstGskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved
                or FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved,
                FirstGskuIdentityWorkflowCheckpoint.WithdrawalObserved
                or FirstGskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown
                or FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
                or FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown,
                FirstGskuIdentityWorkflowCheckpoint.WithdrawalObserved
                or FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
                or FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                FirstGskuIdentityWorkflowCheckpoint.WithdrawalObserved
                or FirstGskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown
                or FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision) => true,
            (FirstGskuIdentityWorkflowCheckpoint.WithdrawalObserved,
                FirstGskuIdentityWorkflowCheckpoint.WithdrawalApplied
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.WithdrawalApplied,
                FirstGskuIdentityWorkflowCheckpoint.Completed
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
                FirstGskuIdentityWorkflowCheckpoint.DecisionObserved) => true,
            // FIX2 item 1: a submit that never reached the engine ends (the draft opens); a manual one whose start the
            // engine does have goes on as started (the submitter's reconciling repeat, CT decision C(A)).
            (FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown
                or FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
                FirstGskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart) => true,
            (FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
                FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted) => true,
            // FIX3 E2: the submitter's reconciling repeat after the start puts a pending pair back to its decision;
            // the requester's repeat of the same withdrawal reads the engine's cancellation again.
            (FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
                FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision
                or FirstGskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown
                // FIX4 item 5: a decision on record goes back to the gate the pair is at.
                or FirstGskuIdentityWorkflowCheckpoint.RevisionApproved
                or FirstGskuIdentityWorkflowCheckpoint.PairApproved
                or FirstGskuIdentityWorkflowCheckpoint.GskuDraftRestored
                or FirstGskuIdentityWorkflowCheckpoint.PairDraftRestored) => true,
            _ => false
        };

    private static bool IsMutationCoherent(
        FirstGskuIdentityWorkflowCheckpoint current,
        FirstGskuIdentityWorkflowCheckpointMutation mutation)
    {
        var hasStart = mutation.WorkflowInstanceId.HasValue || mutation.WorkflowTemplateId.HasValue
            || mutation.WorkflowTemplateVersionId.HasValue || mutation.ApprovalTaskId.HasValue
            || mutation.AssignmentSnapshotId.HasValue || mutation.StartTransitionLogId.HasValue
            || mutation.WorkflowStartedAtUtcTicksV1.HasValue;
        var completeStart = mutation.WorkflowInstanceId is { } workflowId && workflowId != Guid.Empty
            && mutation.WorkflowTemplateId is { } templateId && templateId != Guid.Empty
            && mutation.WorkflowTemplateVersionId is { } templateVersionId && templateVersionId != Guid.Empty
            && mutation.ApprovalTaskId is { } taskId && taskId != Guid.Empty
            && mutation.AssignmentSnapshotId is { } snapshotId && snapshotId != Guid.Empty
            && mutation.StartTransitionLogId is { } logId && logId != Guid.Empty
            && mutation.WorkflowStartedAtUtcTicksV1 is > 0;
        var hasDecision = mutation.DecisionTransitionLogId.HasValue || mutation.DecisionKind.HasValue
            || mutation.DecisionObservedAtUtcTicksV1.HasValue || mutation.DecisionActorSubjectId.HasValue
            || mutation.DecisionReasonCode is not null || mutation.DecisionObjectType is not null
            || mutation.DecisionObjectId is not null || mutation.DecisionObjectRef is not null
            || mutation.DecisionWorkflowTemplateId.HasValue || mutation.DecisionWorkflowTemplateVersionId.HasValue
            || mutation.DecisionTaskStatus is not null || mutation.DecisionInstanceStatus is not null
            || mutation.DecisionTransitionSequence.HasValue || mutation.DecisionAtUtcTicksV1.HasValue;
        var completeDecision = mutation.DecisionKind is ProductIdentityDecisionKind.Approved or ProductIdentityDecisionKind.Rejected
            && mutation.DecisionObservedAtUtcTicksV1 is > 0
            && mutation.DecisionActorSubjectId is { } actor && actor != Guid.Empty
            && mutation.DecisionObjectType == "gsku"
            && Guid.TryParseExact(mutation.DecisionObjectId, "D", out var decisionObjectId) && decisionObjectId != Guid.Empty
            && !string.IsNullOrWhiteSpace(mutation.DecisionObjectRef)
            && mutation.DecisionWorkflowTemplateId is { } decisionTemplate && decisionTemplate != Guid.Empty
            && mutation.DecisionWorkflowTemplateVersionId is { } decisionVersion && decisionVersion != Guid.Empty
            && mutation.DecisionTransitionSequence is > 0 && mutation.DecisionAtUtcTicksV1 is > 0
            && (mutation.DecisionKind == ProductIdentityDecisionKind.Approved
                ? mutation.DecisionTaskStatus == "Approved" && mutation.DecisionInstanceStatus == "Completed"
                : mutation.DecisionTaskStatus == "Rejected" && mutation.DecisionInstanceStatus == "Rejected");
        var hasApproval = mutation.ApprovalPackApplicabilitySelection is not null
            || mutation.ApprovalPackUomSelection is not null
            || mutation.ReferencesValidatedAtUtcTicksV1.HasValue
            || mutation.ApprovalReferenceProofFingerprint is not null;
        var completeApproval = mutation.ApprovalPackApplicabilitySelection is not null
            && mutation.ApprovalPackUomSelection is not null
            && mutation.ReferencesValidatedAtUtcTicksV1 is > 0
            && mutation.ApprovalReferenceProofFingerprint is { Length: 64 };
        var hasWithdrawalRequest = mutation.WithdrawalCommandId.HasValue
            || mutation.WithdrawalFingerprint is not null
            || mutation.WithdrawalRequesterSubjectId.HasValue
            || mutation.WithdrawalExpectedGskuVersion.HasValue
            || mutation.WithdrawalReasonCode is not null
            || mutation.WithdrawalComment is not null;
        var completeWithdrawalRequest = mutation.WithdrawalCommandId is { } withdrawalCommandId
            && withdrawalCommandId != Guid.Empty
            && mutation.WithdrawalRequesterSubjectId is { } withdrawalRequester
            && withdrawalRequester != Guid.Empty
            && mutation.WithdrawalExpectedGskuVersion is > 0
            && IsExactBounded(mutation.WithdrawalFingerprint, 256)
            && IsExactBounded(mutation.WithdrawalReasonCode, 128)
            && (mutation.WithdrawalComment is null || IsExactBounded(mutation.WithdrawalComment, 2000));
        var hasWithdrawalPreflight = mutation.WithdrawalExpectedWorkflowInstanceVersion.HasValue
            || mutation.WithdrawalExpectedApprovalTaskVersion.HasValue
            || mutation.WithdrawalObjectRef is not null;
        var completeWithdrawalPreflight = mutation.WithdrawalExpectedWorkflowInstanceVersion is > 0
            && mutation.WithdrawalExpectedApprovalTaskVersion is > 0
            && IsExactBounded(mutation.WithdrawalObjectRef, 256);
        var hasWithdrawalEvidence = mutation.WithdrawalTransitionLogId.HasValue
            || mutation.WithdrawalObservedAtUtcTicksV1.HasValue
            || mutation.WithdrawalTransitionSequence.HasValue
            || mutation.WithdrawalResultWorkflowInstanceVersion.HasValue
            || mutation.WithdrawalResultApprovalTaskVersion.HasValue
            || mutation.WithdrawalTaskStatus is not null
            || mutation.WithdrawalInstanceStatus is not null;
        var completeWithdrawalEvidence = mutation.WithdrawalTransitionLogId is { } withdrawalLogId
            && withdrawalLogId != Guid.Empty
            && mutation.WithdrawalObservedAtUtcTicksV1 is > 0
            && mutation.WithdrawalTransitionSequence is > 0
            && mutation.WithdrawalResultWorkflowInstanceVersion is > 0
            && mutation.WithdrawalResultApprovalTaskVersion is > 0
            && mutation.WithdrawalTaskStatus == "Cancelled"
            && mutation.WithdrawalInstanceStatus == "Cancelled";

        if (current == mutation.NextCheckpoint)
        {
            // Staying where it is: a retry, a wait through a service fault, a lease given back. It may carry the
            // fault fields, never a fact of another step.
            return mutation.ReleaseLease && !hasStart && !hasDecision && !hasApproval
                && !hasWithdrawalRequest && !hasWithdrawalPreflight && !hasWithdrawalEvidence
                && !mutation.ClearWithdrawal;
        }
        if (current == FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired
            && mutation.NextCheckpoint == FirstGskuIdentityWorkflowCheckpoint.DecisionObserved)
        {
            return mutation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.None
                && mutation.NextAttemptAtUtcTicksV1 is null
                && mutation.LastFailureCode is null
                && mutation.ReleaseLease
                && !hasStart && !hasDecision && !hasApproval;
        }
        if (mutation.NextCheckpoint == FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted)
        {
            return completeStart && !hasDecision && !hasApproval;
        }
        if (mutation.NextCheckpoint == FirstGskuIdentityWorkflowCheckpoint.DecisionObserved)
        {
            return completeDecision && !hasStart && !hasApproval;
        }
        if (mutation.NextCheckpoint == FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated)
        {
            return completeApproval && !hasStart && !hasDecision;
        }
        if (mutation.NextCheckpoint == FirstGskuIdentityWorkflowCheckpoint.WithdrawalRequested)
        {
            return completeWithdrawalRequest && !hasWithdrawalPreflight && !hasWithdrawalEvidence
                && !hasStart && !hasDecision && !hasApproval;
        }
        if (mutation.NextCheckpoint == FirstGskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved)
        {
            return completeWithdrawalPreflight && !hasWithdrawalRequest && !hasWithdrawalEvidence
                && !hasStart && !hasDecision && !hasApproval;
        }
        if (mutation.NextCheckpoint == FirstGskuIdentityWorkflowCheckpoint.WithdrawalObserved)
        {
            // The cancellation evidence carries the object reference the engine cancelled under; the processor has
            // always written it here. Refusing it made every GSKU withdrawal end in a concurrency conflict against
            // the real store (found by the first HTTP test, WP-MDM-GSKU-ACCEPT-01 item 12; the fakes never checked).
            // The versions read before the cancellation still belong to the preflight step only.
            return completeWithdrawalEvidence && !hasWithdrawalRequest
                && !mutation.WithdrawalExpectedWorkflowInstanceVersion.HasValue
                && !mutation.WithdrawalExpectedApprovalTaskVersion.HasValue
                && (mutation.WithdrawalObjectRef is null || IsExactBounded(mutation.WithdrawalObjectRef, 256))
                && !hasStart && !hasDecision && !hasApproval;
        }
        return !hasStart && !hasDecision && !hasApproval
            && !hasWithdrawalRequest && !hasWithdrawalPreflight && !hasWithdrawalEvidence;
    }

    private static bool IsExactReplay(
        FirstGskuIdentityWorkflowOperation existing,
        FirstGskuIdentityWorkflowOperation candidate) =>
        existing.OperationId == candidate.OperationId
        && existing.ProductDefinitionRevisionId == candidate.ProductDefinitionRevisionId
        && existing.GskuId == candidate.GskuId
        && existing.ExpectedRevisionVersion == candidate.ExpectedRevisionVersion
        && existing.ExpectedGskuVersion == candidate.ExpectedGskuVersion
        && existing.StartIdempotencyKey == candidate.StartIdempotencyKey
        && existing.OperationFingerprint == candidate.OperationFingerprint;

    private void EnsureIndexes()
    {
        var active = new BsonDocument(nameof(FirstGskuIdentityWorkflowOperation.IsDeleted), false);
        _operations.Indexes.CreateMany(
        [
            new CreateIndexModel<FirstGskuIdentityWorkflowOperation>(
                Builders<FirstGskuIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.OperationId),
                new CreateIndexOptions<FirstGskuIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_first_gsku_identity_workflow_operation" }),
            new CreateIndexModel<FirstGskuIdentityWorkflowOperation>(
                Builders<FirstGskuIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.StartIdempotencyKey),
                new CreateIndexOptions<FirstGskuIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_first_gsku_identity_workflow_start_key" }),
            new CreateIndexModel<FirstGskuIdentityWorkflowOperation>(
                Builders<FirstGskuIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.ProductDefinitionRevisionId)
                    .Ascending(item => item.GskuId).Ascending(item => item.ExpectedRevisionVersion)
                    .Ascending(item => item.ExpectedGskuVersion),
                new CreateIndexOptions<FirstGskuIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_first_gsku_identity_workflow_pair_version" }),
            new CreateIndexModel<FirstGskuIdentityWorkflowOperation>(
                Builders<FirstGskuIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.IsDeleted)
                    .Ascending(item => item.NextAttemptAtUtcTicksV1).Ascending(item => item.OperationId)
                    .Ascending(item => item.Checkpoint).Ascending(item => item.LeaseUntilUtcTicksV1)
                    .Ascending(item => item.CreatedAtUtcTicksV1),
                new CreateIndexOptions { Name = "ix_mdm_first_gsku_identity_workflow_recovery" }),
            new CreateIndexModel<FirstGskuIdentityWorkflowOperation>(
                Builders<FirstGskuIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.GskuId).Ascending(item => item.Checkpoint),
                new CreateIndexOptions<FirstGskuIdentityWorkflowOperation> { PartialFilterExpression = active,
                    Name = "ix_mdm_first_gsku_identity_workflow_gsku_unfinished" })
        ]);
    }
}
