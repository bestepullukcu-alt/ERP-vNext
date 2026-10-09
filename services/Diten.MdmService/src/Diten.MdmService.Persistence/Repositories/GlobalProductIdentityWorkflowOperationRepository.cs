using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class GlobalProductIdentityWorkflowOperationRepository
    : IGlobalProductIdentityWorkflowOperationRepository
{
    // BL-552 flow 3 FIX2: the lifecycle request documents' class maps (ignore extra elements, checked) are registered
    // before this store is first used — whoever builds it, the host's container or a test. Without it, a store built
    // outside the container auto-maps the type first and the start-up check then refuses that map.
    static GlobalProductIdentityWorkflowOperationRepository() => Diten.MdmService.Persistence.Configurations.GlobalProductLifecycleClassMaps.Register();

    public const string CollectionName = "mdm_global_product_identity_workflow_operations";
    public const string DraftChanged = "PRODUCT_IDENTITY_WORKFLOW_DRAFT_CHANGED";
    public const string IdentityReconciliationRequired = GlobalProductRepository.IdentityReconciliationRequired;
    private readonly IMongoCollection<GlobalProductIdentityWorkflowOperation> _operations;
    private readonly Guid _tenantId;

    public GlobalProductIdentityWorkflowOperationRepository(
        IMongoDatabase database,
        ITenantContext tenantContext)
    {
        _operations = database.GetCollection<GlobalProductIdentityWorkflowOperation>(CollectionName);
        _products = database.GetCollection<GlobalProduct>("mdm_global_products");
        _client = database.Client;
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    private readonly IMongoCollection<GlobalProduct> _products;
    private readonly IMongoClient _client;

    public async Task<GlobalProductIdentityWorkflowReserveResult> ReserveAsync(
        GlobalProductIdentityWorkflowOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!IsValidForReservation(operation))
        {
            return new(false, false, null, "PRODUCT_IDENTITY_WORKFLOW_OPERATION_INVALID");
        }

        operation.TenantId = _tenantId;
        operation.Id = operation.Id == Guid.Empty ? Guid.NewGuid() : operation.Id;
        operation.IsDeleted = false;
        operation.DeletedAt = null;
        operation.Checkpoint = GlobalProductIdentityWorkflowCheckpoint.Prepared;
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
        operation.WithdrawalCommandId = null;
        operation.WithdrawalFingerprint = null;
        operation.WithdrawalRequesterSubjectId = null;
        operation.WithdrawalExpectedProductVersion = null;
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
        operation.NextAttemptAtUtcTicksV1 = null;
        operation.LastFailureCode = null;
        operation.LeaseOwner = null;
        operation.LeaseUntilUtcTicksV1 = null;
        operation.LeaseGeneration = 0;
        operation.TemporalStorageVersion = GlobalProductIdentityWorkflowOperation.CurrentTemporalStorageVersion;
        operation.Version = 0;
        operation.CreatedAtUtcTicksV1 = operation.CreatedAtUtcTicksV1 > 0
            ? operation.CreatedAtUtcTicksV1
            : DateTimeOffset.UtcNow.UtcTicks;
        operation.UpdatedAtUtcTicksV1 = operation.CreatedAtUtcTicksV1;
        operation.CreatedAt = new DateTimeOffset(operation.CreatedAtUtcTicksV1, TimeSpan.Zero);
        operation.UpdatedAt = operation.CreatedAt;

        // WP-MDM-GSKU-ACCEPT-01 FIX3 item 6 (GSKU's FIX2 item 10 pattern): the reservation and a touch of the draft it
        // submits are one transaction; the draft edit reads the latest submit and writes in one transaction too, so the
        // two cannot both commit — either the edit sees this submit (and is refused), or this submit finds the draft
        // moved (and is refused) before any approval is started on facts the product no longer has.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
                session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority));
                try
                {
                    // BL-552 flow 3 (CT decision S2): a submit of this very version stuck in manual reconciliation holds
                    // the draft — a new submit is refused until its submitter's same submit settles it (or a person).
                    var stuck = await _operations.Find(session,
                            ActiveTenantFilter
                            & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.GlobalProductId, operation.GlobalProductId)
                            & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.ExpectedProductVersion, operation.ExpectedProductVersion)
                            & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.Checkpoint,
                                GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired)
                            & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Ne(item => item.OperationId, operation.OperationId))
                        .AnyAsync(cancellationToken);
                    if (stuck)
                    {
                        await session.AbortTransactionAsync(cancellationToken);
                        return new(false, false, null, IdentityReconciliationRequired);
                    }
                    await _operations.InsertOneAsync(session, operation, cancellationToken: cancellationToken);
                    var product = Builders<GlobalProduct>.Filter.Eq(item => item.TenantId, _tenantId)
                                  & Builders<GlobalProduct>.Filter.Eq(item => item.IsDeleted, false)
                                  & Builders<GlobalProduct>.Filter.Eq(item => item.Id, operation.GlobalProductId);
                    var touched = await _products.UpdateOneAsync(session,
                        product
                        & Builders<GlobalProduct>.Filter.Eq(item => item.Version, operation.ExpectedProductVersion)
                        & Builders<GlobalProduct>.Filter.Eq(item => item.LifecycleStatus, ProductIdentityLifecycleStatus.Draft),
                        Builders<GlobalProduct>.Update.Inc(item => item.IdentitySubmitReservations, 1),
                        cancellationToken: cancellationToken);
                    if (touched.MatchedCount != 1 && await _products.Find(session, product).AnyAsync(cancellationToken))
                    {
                        await session.AbortTransactionAsync(cancellationToken);
                        return new(false, false, null, DraftChanged);
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
                // A write conflict with an edit that committed first — or the store itself (S5): read again to say which.
                // FIX4 item 7: — or the SAME submit sent twice, the other committing first: its answer is this one's.
                var competing = await CompetingReservationAsync(operation, cancellationToken);
                if (competing is not null)
                    return IsExactReplay(competing, operation)
                        ? new(true, true, competing, null)
                        : new(false, false, null, "PRODUCT_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT");
                if (await ProductMovedAsync(operation, cancellationToken)) return new(false, false, null, DraftChanged);
                // The other one may not have committed yet: try again, briefly (the identity reservation's rule).
                if (attempt >= ReserveAttempts) return new(false, false, null, ProductIdentityStore.Unavailable);
                await Task.Delay(TimeSpan.FromMilliseconds(50 << attempt), cancellationToken);
            }
            catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                var existing = await CompetingReservationAsync(operation, cancellationToken);
                return existing is not null && IsExactReplay(existing, operation)
                    ? new(true, true, existing, null)
                    : new(false, false, null, "PRODUCT_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT");
            }
            catch (MongoException exception) when (exception is not MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey })
            {
                // FIX3 S5: no transaction at all (a standalone mongod) or a store that is away.
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

    private async Task<GlobalProductIdentityWorkflowOperation?> CompetingReservationAsync(
        GlobalProductIdentityWorkflowOperation operation, CancellationToken cancellationToken)
    {
        try
        {
            return await _operations.Find(ActiveTenantFilter & Builders<GlobalProductIdentityWorkflowOperation>
                    .Filter.Or(
                        Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.OperationId, operation.OperationId),
                        Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                            item => item.StartIdempotencyKey, operation.StartIdempotencyKey),
                        Builders<GlobalProductIdentityWorkflowOperation>.Filter.And(
                            Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                                item => item.GlobalProductId, operation.GlobalProductId),
                            Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                                item => item.ExpectedProductVersion, operation.ExpectedProductVersion))))
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (MongoException)
        {
            return null;
        }
    }

    private async Task<bool> ProductMovedAsync(GlobalProductIdentityWorkflowOperation operation, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _products.Find(Builders<GlobalProduct>.Filter.Eq(item => item.TenantId, _tenantId)
                    & Builders<GlobalProduct>.Filter.Eq(item => item.IsDeleted, false)
                    & Builders<GlobalProduct>.Filter.Eq(item => item.Id, operation.GlobalProductId))
                .FirstOrDefaultAsync(cancellationToken);
            return product is not null && (product.Version != operation.ExpectedProductVersion
                                           || product.LifecycleStatus != ProductIdentityLifecycleStatus.Draft);
        }
        catch (MongoException)
        {
            return false;
        }
    }

    public Task<GlobalProductIdentityWorkflowOperation?> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        _operations.Find(ActiveTenantFilter
                         & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                             item => item.OperationId, operationId))
            .FirstOrDefaultAsync(cancellationToken)!;

    public Task<GlobalProductIdentityWorkflowOperation?> GetByStartIdempotencyKeyAsync(
        string startIdempotencyKey,
        CancellationToken cancellationToken = default) =>
        _operations.Find(ActiveTenantFilter
                         & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                             item => item.StartIdempotencyKey, startIdempotencyKey))
            .FirstOrDefaultAsync(cancellationToken)!;

    public async Task<GlobalProductIdentityWorkflowClaim?> TryClaimAsync(
        GlobalProductIdentityWorkflowClaimRequest request,
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
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationId, request.OperationId)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationFingerprint, request.OperationFingerprint)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.TemporalStorageVersion,
                         GlobalProductIdentityWorkflowOperation.CurrentTemporalStorageVersion)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.In(
                         item => item.Checkpoint, request.EligibleCheckpoints)
                     & (request.IgnoreRetrySchedule
                         ? Builders<GlobalProductIdentityWorkflowOperation>.Filter.Empty
                         : Builders<GlobalProductIdentityWorkflowOperation>.Filter.Or(
                             Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.NextAttemptAtUtcTicksV1, null),
                             Builders<GlobalProductIdentityWorkflowOperation>.Filter.Lte(
                                 item => item.NextAttemptAtUtcTicksV1, request.NowUtcTicks)))
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Or(
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, null),
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Lte(
                             item => item.LeaseUntilUtcTicksV1, request.NowUtcTicks));
        var update = Builders<GlobalProductIdentityWorkflowOperation>.Update
            .Set(item => item.LeaseOwner, request.LeaseOwner)
            .Set(item => item.LeaseUntilUtcTicksV1, request.LeaseUntilUtcTicks)
            .Set(item => item.UpdatedAtUtcTicksV1, request.NowUtcTicks)
            .Set(item => item.UpdatedAt, new DateTimeOffset(request.NowUtcTicks, TimeSpan.Zero))
            .Inc(item => item.LeaseGeneration, 1)
            .Inc(item => item.Version, 1);
        var operation = await _operations.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<GlobalProductIdentityWorkflowOperation>
            {
                ReturnDocument = ReturnDocument.After,
                IsUpsert = false
            },
            cancellationToken);
        return operation is null
            ? null
            : new GlobalProductIdentityWorkflowClaim(
                _tenantId,
                operation.OperationId,
                operation.OperationFingerprint,
                request.LeaseOwner,
                operation.LeaseGeneration,
                operation.Checkpoint,
                request.LeaseUntilUtcTicks);
    }

    public Task ReleaseIfOwnedAsync(
        Guid operationId,
        string leaseOwner,
        long leaseUntilUtcTicks,
        CancellationToken cancellationToken = default) =>
        _operations.UpdateOneAsync(
            ActiveTenantFilter
            & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.OperationId, operationId)
            & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, leaseOwner)
            & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseUntilUtcTicksV1, leaseUntilUtcTicks),
            Builders<GlobalProductIdentityWorkflowOperation>.Update
                .Set(item => item.LeaseOwner, null)
                .Set(item => item.LeaseUntilUtcTicksV1, null),
            cancellationToken: cancellationToken);

    public async Task<GlobalProductIdentityWorkflowRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks,
        int limit,
        GlobalProductIdentityWorkflowRecoveryCursor? after = null,
        CancellationToken cancellationToken = default)
    {
        if (nowUtcTicks <= 0 || limit is < 1 or > 100 || after?.OperationId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var terminal = new[]
        {
            GlobalProductIdentityWorkflowCheckpoint.Completed,
            GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            GlobalProductIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
            GlobalProductIdentityWorkflowCheckpoint.Superseded
        };
        var filter = ActiveTenantFilter
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.TemporalStorageVersion,
                         GlobalProductIdentityWorkflowOperation.CurrentTemporalStorageVersion)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Nin(item => item.Checkpoint, terminal)
                     // AwaitingMakerReplay is the maker's to continue — EXCEPT a withdrawal an earlier version
                     // parked there: its approval exists at the engine, so the worker must keep reading it.
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Or(
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Ne(
                             item => item.Checkpoint, GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay),
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Ne(
                             item => item.WithdrawalCommandId, null))
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Or(
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.NextAttemptAtUtcTicksV1, null),
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Lte(
                             item => item.NextAttemptAtUtcTicksV1, nowUtcTicks))
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Or(
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, null),
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Lte(
                             item => item.LeaseUntilUtcTicksV1, nowUtcTicks));
        if (after is not null)
        {
            filter &= after.NextAttemptAtUtcTicksV1.HasValue
                ? Builders<GlobalProductIdentityWorkflowOperation>.Filter.Or(
                    Builders<GlobalProductIdentityWorkflowOperation>.Filter.Gt(
                        item => item.NextAttemptAtUtcTicksV1, after.NextAttemptAtUtcTicksV1),
                    Builders<GlobalProductIdentityWorkflowOperation>.Filter.And(
                        Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                            item => item.NextAttemptAtUtcTicksV1, after.NextAttemptAtUtcTicksV1),
                        Builders<GlobalProductIdentityWorkflowOperation>.Filter.Gt(
                            item => item.OperationId, after.OperationId)))
                : Builders<GlobalProductIdentityWorkflowOperation>.Filter.Or(
                    Builders<GlobalProductIdentityWorkflowOperation>.Filter.Ne(
                        item => item.NextAttemptAtUtcTicksV1, null),
                    Builders<GlobalProductIdentityWorkflowOperation>.Filter.And(
                        Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                            item => item.NextAttemptAtUtcTicksV1, null),
                        Builders<GlobalProductIdentityWorkflowOperation>.Filter.Gt(
                            item => item.OperationId, after.OperationId)));
        }

        var rows = await _operations.Find(filter)
            .SortBy(item => item.NextAttemptAtUtcTicksV1)
            .ThenBy(item => item.OperationId)
            .Limit(limit)
            .ToListAsync(cancellationToken);
        var next = rows.Count == limit
            ? new GlobalProductIdentityWorkflowRecoveryCursor(
                rows[^1].NextAttemptAtUtcTicksV1,
                rows[^1].OperationId)
            : null;
        return new(rows, next);
    }

    public async Task<bool> AdvanceAsync(
        GlobalProductIdentityWorkflowClaim claim,
        GlobalProductIdentityWorkflowCheckpointMutation mutation,
        CancellationToken cancellationToken = default)
    {
        if (claim.TenantId != _tenantId
            || mutation.UpdatedAtUtcTicks <= 0
            || !Accepts(claim.Checkpoint, mutation))
        {
            return false;
        }

        var filter = ActiveTenantFilter
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationId, claim.OperationId)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationFingerprint, claim.OperationFingerprint)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.Checkpoint, claim.Checkpoint)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.LeaseOwner, claim.LeaseOwner)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.LeaseGeneration, claim.LeaseGeneration)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Gt(
                         item => item.LeaseUntilUtcTicksV1, mutation.UpdatedAtUtcTicks);
        var update = Builders<GlobalProductIdentityWorkflowOperation>.Update
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
        if (mutation.WithdrawalCommandId.HasValue) update = update.Set(item => item.WithdrawalCommandId, mutation.WithdrawalCommandId);
        if (mutation.WithdrawalFingerprint is not null) update = update.Set(item => item.WithdrawalFingerprint, mutation.WithdrawalFingerprint);
        if (mutation.WithdrawalRequesterSubjectId.HasValue) update = update.Set(item => item.WithdrawalRequesterSubjectId, mutation.WithdrawalRequesterSubjectId);
        if (mutation.WithdrawalExpectedProductVersion.HasValue) update = update.Set(item => item.WithdrawalExpectedProductVersion, mutation.WithdrawalExpectedProductVersion);
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
        if (mutation.WithdrawalAwaitingRequesterSinceUtcTicksV1.HasValue) update = update.Set(item => item.WithdrawalAwaitingRequesterSinceUtcTicksV1, mutation.WithdrawalAwaitingRequesterSinceUtcTicksV1);
        if (mutation.WithdrawalRefusalCode is not null) update = update.Set(item => item.WithdrawalRefusalCode, mutation.WithdrawalRefusalCode);
        if (mutation.ServiceFaultCount.HasValue)
        {
            update = update
                .Set(item => item.ServiceFaultCount, mutation.ServiceFaultCount.Value)
                .Set(item => item.ServiceFaultSinceUtcTicksV1, mutation.ServiceFaultSinceUtcTicksV1);
        }
        update = mutation.Streak switch
        {
            RecoveryStreakWrite.Increment => update.Inc(item => item.FailedAttemptCount, 1),
            RecoveryStreakWrite.Keep => update,
            _ => update.Set(item => item.FailedAttemptCount, 0)
        };
        if (mutation.LastStartSentAtUtcTicksV1.HasValue)
            update = update.Set(item => item.LastStartSentAtUtcTicksV1, mutation.LastStartSentAtUtcTicksV1);
        if (mutation.ClearWithdrawal)
        {
            update = update
                .Set(item => item.WithdrawalCommandId, null)
                .Set(item => item.WithdrawalFingerprint, null)
                .Set(item => item.WithdrawalRequesterSubjectId, null)
                .Set(item => item.WithdrawalExpectedProductVersion, null)
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

    // BL-552: only what is STUCK — manual reconciliation held for a person. (A service fault is never quarantined in
    // this flow: it waits, said by its own fault streak; a submit that ended is not counted.)
    public Task<long> CountManualReconciliationAsync(CancellationToken cancellationToken = default) =>
        _operations.CountDocumentsAsync(ActiveTenantFilter
            & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.Checkpoint, GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired)
            & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired),
            cancellationToken: cancellationToken);

    // BL-552: stuck for a reason the submitter's repeat cannot settle — a person has to (BL-530).
    public Task<long> CountNeedingAPersonAsync(CancellationToken cancellationToken = default) =>
        _operations.CountDocumentsAsync(ActiveTenantFilter
            & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.Checkpoint, GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired)
            & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
            & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Nin(item => item.LastFailureCode,
                Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.GlobalProductIdentityWorkflowProcessor.SettledBySubmittersRepeat),
            cancellationToken: cancellationToken);

    private FilterDefinition<GlobalProductIdentityWorkflowOperation> ActiveTenantFilter =>
        Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.TenantId, _tenantId)
        & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.IsDeleted, false);

    private bool IsValidForReservation(GlobalProductIdentityWorkflowOperation operation) =>
        _tenantId != Guid.Empty
        && operation.OperationId != Guid.Empty
        && operation.GlobalProductId != Guid.Empty
        && operation.ExpectedProductVersion >= 0
        && operation.MakerSubjectId != Guid.Empty
        && (operation.WorkflowTemplateId.HasValue ^ operation.WorkflowTemplateCode is not null)
        && operation.CandidatePrincipalIds is { Count: >= 1 and <= 100 }
        && operation.CandidatePrincipalIds.All(item => item != Guid.Empty)
        && operation.CandidatePrincipalIds.Distinct().Count() == operation.CandidatePrincipalIds.Count
        && IsExactBounded(operation.ReasonCode, 128)
        && operation.ObjectType == "GlobalProduct"
        && operation.ObjectId == operation.GlobalProductId.ToString("D")
        && IsExactBounded(operation.ObjectRef, 256)
        && IsExactBounded(operation.StartIdempotencyKey, 256)
        && IsExactBounded(operation.OperationFingerprint, 256)
        && (!operation.WorkflowTemplateId.HasValue || operation.WorkflowTemplateId != Guid.Empty)
        && (operation.WorkflowTemplateCode is null || IsExactBounded(operation.WorkflowTemplateCode, 128))
        && operation.TemporalStorageVersion is 0 or GlobalProductIdentityWorkflowOperation.CurrentTemporalStorageVersion;

    private static bool IsExactBounded(string? value, int maximumLength) =>
        value is { Length: > 0 }
        && value.Length <= maximumLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    /// <summary>
    /// Every rule this store applies to a write's checkpoint (flow 3 FIX1 item 9 — public so the processor's
    /// transition census measures the very rules production runs).
    /// </summary>
    public static bool Accepts(
        GlobalProductIdentityWorkflowCheckpoint current,
        GlobalProductIdentityWorkflowCheckpointMutation mutation) =>
        IsAllowedTransition(current, mutation.NextCheckpoint)
        // BL-552 (the GSKU FIX7 store invariant): manual reconciliation is never "retryable" — the worker does not scan
        // it and the gauge counts it only when it is held for a person: the pair would be a submit nobody sees.
        // Flow 3 FIX1 item 5: nor any other disposition (None, AwaitingMakerReplay) — such a row would hold the draft
        // (S2) while no gauge counts it, no discovery finds it and no repeat reconciles it. Global Product's identity
        // flow ends a submit at Completed, never at Manual + None.
        && (mutation.NextCheckpoint != GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired
            || mutation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
        // Flow 3 FIX1 item 9: a submit awaiting its decision is COMPLETED from there only by ending — an approval closed
        // without a decision or decided by its own maker (the product a draft again). Any other completion goes
        // through its decision.
        && (current != GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision
            || mutation.NextCheckpoint != GlobalProductIdentityWorkflowCheckpoint.Completed
            || mutation.LastFailureCode is GlobalProductIdentityWorkflowProcessor.ClosedWithoutDecision
                or GlobalProductIdentityWorkflowProcessor.SelfApprovalIgnored);

    public static bool IsAllowedTransition(
        GlobalProductIdentityWorkflowCheckpoint current,
        GlobalProductIdentityWorkflowCheckpoint next) =>
        current == next
        || (current, next) switch
        {
            (GlobalProductIdentityWorkflowCheckpoint.Prepared,
                GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown) => true,
            (GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted
                or GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            // A maker's resubmit whose outcome is unknown again goes back to the worker's lookup.
            (GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown) => true,
            (GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted,
                GlobalProductIdentityWorkflowCheckpoint.LocalPendingApplied
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            // Flow 3 FIX1 item 9 (the census): the step after the pending write only moves on — nothing at
            // LocalPendingApplied is stuck.
            (GlobalProductIdentityWorkflowCheckpoint.LocalPendingApplied,
                GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision) => true,
            (GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision,
                GlobalProductIdentityWorkflowCheckpoint.DecisionObserved
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductIdentityWorkflowCheckpoint.DecisionObserved,
                GlobalProductIdentityWorkflowCheckpoint.DecisionApplied
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductIdentityWorkflowCheckpoint.DecisionApplied,
                GlobalProductIdentityWorkflowCheckpoint.Completed
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision,
                GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested) => true,
            // Flow 3 FIX1 item 9 (the census): a withdrawal no longer parks in AwaitingMakerReplay; only rows an
            // earlier version parked there are read back out of it (below).
            (GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested,
                GlobalProductIdentityWorkflowCheckpoint.WithdrawalPreflightObserved
                or GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductIdentityWorkflowCheckpoint.WithdrawalPreflightObserved,
                GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown
                or GlobalProductIdentityWorkflowCheckpoint.WithdrawalObserved
                or GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown,
                GlobalProductIdentityWorkflowCheckpoint.WithdrawalObserved
                or GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            // A withdrawal an earlier version parked here, repeated: it ends where a first one does — cancelled, or
            // already decided — or its outcome is unknown again.
            (GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown
                or GlobalProductIdentityWorkflowCheckpoint.WithdrawalObserved
                or GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision) => true,
            (GlobalProductIdentityWorkflowCheckpoint.WithdrawalObserved,
                GlobalProductIdentityWorkflowCheckpoint.WithdrawalApplied
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductIdentityWorkflowCheckpoint.WithdrawalApplied,
                GlobalProductIdentityWorkflowCheckpoint.Completed
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            // BL-552: an approval closed without an applicable decision ends here — the product is a draft again.
            (GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, GlobalProductIdentityWorkflowCheckpoint.Completed) => true,
            // BL-552: the submitter's same command reads the engine again and puts a stuck submit back where it
            // stands (started, pending, decided, or — nothing arrived — the maker's to resubmit).
            (GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired,
                GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted or GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision
                or GlobalProductIdentityWorkflowCheckpoint.DecisionObserved or GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay) => true,
            _ => false
        };

    private static bool IsExactReplay(
        GlobalProductIdentityWorkflowOperation existing,
        GlobalProductIdentityWorkflowOperation candidate) =>
        existing.OperationId == candidate.OperationId
        && existing.GlobalProductId == candidate.GlobalProductId
        && existing.ExpectedProductVersion == candidate.ExpectedProductVersion
        && existing.StartIdempotencyKey == candidate.StartIdempotencyKey
        && existing.OperationFingerprint == candidate.OperationFingerprint;

    public const string StuckIndexName = "ix_mdm_gp_identity_workflow_stuck";

    private void EnsureIndexes()
    {
        var active = new BsonDocument(nameof(GlobalProductIdentityWorkflowOperation.IsDeleted), false);
        _operations.Indexes.CreateMany(
        [
            new CreateIndexModel<GlobalProductIdentityWorkflowOperation>(
                Builders<GlobalProductIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.OperationId),
                new CreateIndexOptions<GlobalProductIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_gp_identity_workflow_operation" }),
            new CreateIndexModel<GlobalProductIdentityWorkflowOperation>(
                Builders<GlobalProductIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.StartIdempotencyKey),
                new CreateIndexOptions<GlobalProductIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_gp_identity_workflow_start_key" }),
            new CreateIndexModel<GlobalProductIdentityWorkflowOperation>(
                Builders<GlobalProductIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.GlobalProductId)
                    .Ascending(item => item.ExpectedProductVersion),
                new CreateIndexOptions<GlobalProductIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_gp_identity_workflow_product_version" }),
            new CreateIndexModel<GlobalProductIdentityWorkflowOperation>(
                Builders<GlobalProductIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.IsDeleted)
                    .Ascending(item => item.NextAttemptAtUtcTicksV1).Ascending(item => item.OperationId)
                    .Ascending(item => item.Checkpoint).Ascending(item => item.LeaseUntilUtcTicksV1)
                    .Ascending(item => item.CreatedAtUtcTicksV1),
                new CreateIndexOptions { Name = "ix_mdm_gp_identity_workflow_recovery" }),
            // BL-552 flow 3 FIX3 item 1: only the STUCK rows, by tenant — the stuck-tenant scan and the gauges' counts
            // read this, not every finished submit. Partial: it stays as small as what is stuck.
            new CreateIndexModel<GlobalProductIdentityWorkflowOperation>(
                Builders<GlobalProductIdentityWorkflowOperation>.IndexKeys.Ascending(item => item.TenantId),
                new CreateIndexOptions<GlobalProductIdentityWorkflowOperation>
                {
                    Name = StuckIndexName,
                    PartialFilterExpression = GlobalProductIdentityWorkflowTenantPartitionDiscoveryRepository.StuckPartialFilter
                })
        ]);
    }
}
