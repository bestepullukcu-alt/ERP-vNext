using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public sealed class FirstGskuIdentityRetirementProcessor(
    IFirstGskuIdentityRetirementOperationRepository operations,
    IGskuRepository gskus,
    IProductDefinitionRevisionRepository revisions,
    TimeProvider timeProvider)
{
    private static readonly FirstGskuIdentityRetirementCheckpoint[] Claimable =
    [
        FirstGskuIdentityRetirementCheckpoint.Prepared,
        FirstGskuIdentityRetirementCheckpoint.AdmissionFenceClosed,
        FirstGskuIdentityRetirementCheckpoint.ChildrenVerified,
        FirstGskuIdentityRetirementCheckpoint.GskuRetired,
        FirstGskuIdentityRetirementCheckpoint.RevisionRetired
    ];

    public async Task<GskuPairRetirementProcessingResult> StartAsync(
        Guid gskuId,
        int expectedGskuVersion,
        Guid operationId,
        Guid actorId,
        string reasonCode,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        if (gskuId == Guid.Empty || expectedGskuVersion < 0 || operationId == Guid.Empty
            || actorId == Guid.Empty || !Exact(reasonCode, 128) || !Exact(leaseOwner, 128)
            || leaseDuration <= TimeSpan.Zero)
        {
            return Fail("FIRST_GSKU_RETIREMENT_REQUEST_INVALID", 400);
        }

        var existing = await operations.GetByOperationIdAsync(operationId, cancellationToken);
        if (existing is not null)
        {
            if (existing.GskuId != gskuId || existing.ExpectedGskuVersion != expectedGskuVersion
                || existing.ActorSubjectId != actorId || existing.ReasonCode != reasonCode)
            {
                return Fail(existing, "FIRST_GSKU_RETIREMENT_OPERATION_CONFLICT", 409);
            }
            return await ProcessAsync(existing, leaseOwner, leaseDuration, cancellationToken, true);
        }

        var gsku = await gskus.GetByIdAsync(gskuId, cancellationToken);
        var revision = gsku is null
            ? null
            : await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        if (gsku is null || revision is null || gsku.IsDeleted || revision.IsDeleted
            || gsku.TenantId == Guid.Empty || revision.TenantId != gsku.TenantId)
        {
            return Fail("GSKU_IDENTITY_NOT_FOUND", 404);
        }
        if (gsku.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved
            || revision.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved
            || gsku.Version != expectedGskuVersion
            || !ExactPairBinding(gsku, revision))
        {
            return Fail("FIRST_GSKU_RETIREMENT_STATE_CONFLICT", 409);
        }

        var now = timeProvider.GetUtcNow();
        var fingerprint = Fingerprint(
            gsku.TenantId, operationId, revision.Id, gsku.Id, revision.Version,
            expectedGskuVersion, actorId, reasonCode);
        var operation = new FirstGskuIdentityRetirementOperation
        {
            Id = operationId,
            TenantId = gsku.TenantId,
            OperationId = operationId,
            OperationFingerprint = fingerprint,
            ProductDefinitionRevisionId = revision.Id,
            GskuId = gsku.Id,
            ExpectedRevisionVersion = revision.Version,
            ExpectedGskuVersion = expectedGskuVersion,
            ActorSubjectId = actorId,
            ReasonCode = reasonCode,
            Checkpoint = FirstGskuIdentityRetirementCheckpoint.Prepared,
            RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None,
            TemporalStorageVersion = FirstGskuIdentityRetirementOperation.CurrentTemporalStorageVersion,
            CreatedAtUtcTicksV1 = now.UtcTicks,
            UpdatedAtUtcTicksV1 = now.UtcTicks
        };
        var reserved = await operations.ReserveAsync(operation, cancellationToken);
        if (!reserved.Succeeded || reserved.Operation is null)
        {
            return Fail(reserved.ErrorCode ?? "FIRST_GSKU_RETIREMENT_OPERATION_CONFLICT", 409);
        }
        return await ProcessAsync(
            reserved.Operation, leaseOwner, leaseDuration, cancellationToken, reserved.IsReplay);
    }

    public Task<GskuPairRetirementProcessingResult> RecoverAsync(
        FirstGskuIdentityRetirementOperation operation,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default) =>
        ProcessAsync(operation, leaseOwner, leaseDuration, cancellationToken, true);

    private async Task<GskuPairRetirementProcessingResult> ProcessAsync(
        FirstGskuIdentityRetirementOperation initial,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken,
        bool replay)
    {
        var operation = initial;
        for (var step = 0; step < 8; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.Checkpoint == FirstGskuIdentityRetirementCheckpoint.Completed)
            {
                return Success(operation, replay);
            }
            if (operation.Checkpoint == FirstGskuIdentityRetirementCheckpoint.ManualReconciliationRequired)
            {
                return Fail(operation, operation.LastFailureCode
                    ?? "FIRST_GSKU_RETIREMENT_RECONCILIATION_REQUIRED", 409);
            }

            var now = timeProvider.GetUtcNow();
            var claim = await operations.TryClaimAsync(new(
                operation.OperationId,
                operation.OperationFingerprint,
                Claimable,
                leaseOwner,
                now.UtcTicks,
                now.Add(leaseDuration).UtcTicks), cancellationToken);
            if (claim is null)
            {
                return Fail(operation, "FIRST_GSKU_RETIREMENT_BUSY", 409);
            }

            var advanced = operation.Checkpoint switch
            {
                FirstGskuIdentityRetirementCheckpoint.Prepared =>
                    await CloseFenceAsync(operation, claim, now, cancellationToken),
                FirstGskuIdentityRetirementCheckpoint.AdmissionFenceClosed =>
                    await VerifyChildrenAsync(operation, claim, now, cancellationToken),
                FirstGskuIdentityRetirementCheckpoint.ChildrenVerified =>
                    await RetireGskuAsync(operation, claim, now, cancellationToken),
                FirstGskuIdentityRetirementCheckpoint.GskuRetired =>
                    await RetireRevisionAsync(operation, claim, now, cancellationToken),
                FirstGskuIdentityRetirementCheckpoint.RevisionRetired =>
                    await VerifyAndCompleteAsync(operation, claim, now, cancellationToken),
                _ => false
            };
            if (!advanced)
            {
                return Fail(operation, "FIRST_GSKU_RETIREMENT_CONCURRENCY_CONFLICT", 409);
            }
            operation = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken)
                ?? throw new InvalidOperationException("FIRST_GSKU_RETIREMENT_OPERATION_LOST");
            if (operation.LastFailureCode == "FIRST_GSKU_RETIREMENT_CHILD_BLOCKED")
            {
                return Fail(operation, operation.LastFailureCode, 409);
            }
        }
        return Fail(operation, "FIRST_GSKU_RETIREMENT_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> CloseFenceAsync(
        FirstGskuIdentityRetirementOperation operation,
        FirstGskuIdentityRetirementClaim claim,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var result = await gskus.CloseChildAdmissionFenceAsync(
            operation.GskuId, operation.ExpectedGskuVersion, operation.OperationId,
            operation.OperationFingerprint, cancellationToken);
        return result.Succeeded && ExactFencedGsku(result.Aggregate, operation)
            ? await AdvanceAsync(claim, FirstGskuIdentityRetirementCheckpoint.AdmissionFenceClosed,
                now, cancellationToken: cancellationToken)
            : await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private async Task<bool> VerifyChildrenAsync(
        FirstGskuIdentityRetirementOperation operation,
        FirstGskuIdentityRetirementClaim claim,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await gskus.HasNonRetiredSiblingAsync(
                operation.ProductDefinitionRevisionId, operation.GskuId, cancellationToken))
        {
            return await ScheduleBlockedRetryAsync(
                claim, now, "FIRST_GSKU_RETIREMENT_CHILD_BLOCKED", cancellationToken);
        }
        var blocker = await gskus.FindRetirementBlockerAsync(operation.GskuId, cancellationToken);
        if (blocker is not null)
        {
            return await ScheduleBlockedRetryAsync(
                claim, now, "FIRST_GSKU_RETIREMENT_CHILD_BLOCKED", cancellationToken);
        }
        return await AdvanceAsync(claim, FirstGskuIdentityRetirementCheckpoint.ChildrenVerified,
            now, cancellationToken: cancellationToken);
    }

    private async Task<bool> RetireGskuAsync(
        FirstGskuIdentityRetirementOperation operation,
        FirstGskuIdentityRetirementClaim claim,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var gsku = await gskus.GetByIdAsync(operation.GskuId, cancellationToken);
        if (!ExactFencedGsku(gsku, operation) && !ExactRetiredGsku(gsku, operation))
        {
            return await QuarantineAsync(claim, "FIRST_GSKU_RETIREMENT_PAIR_INCONSISTENT", now, cancellationToken);
        }
        var intent = FirstGskuIdentityRetirementAuditIntentFactory.CreateGsku(
            gsku!, operation.ExpectedGskuVersion + 1, operation.OperationId,
            operation.OperationFingerprint, operation.ActorSubjectId, operation.ReasonCode,
            Timestamp(operation));
        var result = await gskus.RetireIdentityAsync(
            operation.GskuId, operation.ExpectedGskuVersion + 1, operation.OperationId,
            operation.OperationFingerprint, intent, cancellationToken);
        return result.Succeeded && ExactRetiredGsku(result.Aggregate, operation)
            ? await AdvanceAsync(claim, FirstGskuIdentityRetirementCheckpoint.GskuRetired,
                now, cancellationToken: cancellationToken)
            : await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private async Task<bool> RetireRevisionAsync(
        FirstGskuIdentityRetirementOperation operation,
        FirstGskuIdentityRetirementClaim claim,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await gskus.HasNonRetiredSiblingAsync(
                operation.ProductDefinitionRevisionId, operation.GskuId, cancellationToken))
        {
            return await QuarantineAsync(
                claim, "FIRST_GSKU_RETIREMENT_SIBLING_RACE", now, cancellationToken);
        }
        var revision = await revisions.GetByIdAsync(operation.ProductDefinitionRevisionId, cancellationToken);
        if (!ExactApprovedRevision(revision, operation) && !ExactRetiredRevision(revision, operation))
        {
            return await QuarantineAsync(claim, "FIRST_GSKU_RETIREMENT_PAIR_INCONSISTENT", now, cancellationToken);
        }
        var intent = FirstGskuIdentityRetirementAuditIntentFactory.CreateRevision(
            revision!, operation.ExpectedRevisionVersion, operation.OperationId,
            operation.OperationFingerprint, operation.ActorSubjectId, operation.ReasonCode,
            Timestamp(operation));
        var result = await revisions.RetireIdentityAsync(
            operation.ProductDefinitionRevisionId, operation.ExpectedRevisionVersion,
            operation.OperationId, operation.OperationFingerprint, intent, cancellationToken);
        return result.Succeeded && ExactRetiredRevision(result.Aggregate, operation)
            ? await AdvanceAsync(claim, FirstGskuIdentityRetirementCheckpoint.RevisionRetired,
                now, cancellationToken: cancellationToken)
            : await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private async Task<bool> VerifyAndCompleteAsync(
        FirstGskuIdentityRetirementOperation operation,
        FirstGskuIdentityRetirementClaim claim,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var gsku = await gskus.GetByIdAsync(operation.GskuId, cancellationToken);
        var revision = await revisions.GetByIdAsync(operation.ProductDefinitionRevisionId, cancellationToken);
        return ExactRetiredGsku(gsku, operation) && ExactRetiredRevision(revision, operation)
            && ExactRetirementAudit(gsku!, revision!, operation)
            ? await AdvanceAsync(claim, FirstGskuIdentityRetirementCheckpoint.Completed,
                now, cancellationToken: cancellationToken)
            : await QuarantineAsync(claim, "FIRST_GSKU_RETIREMENT_PAIR_INCONSISTENT", now, cancellationToken);
    }

    private Task<bool> AdvanceAsync(
        FirstGskuIdentityRetirementClaim claim,
        FirstGskuIdentityRetirementCheckpoint checkpoint,
        DateTimeOffset now,
        string? failure = null,
        CancellationToken cancellationToken = default) =>
        operations.AdvanceAsync(claim, new(
            checkpoint, ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
            LastFailureCode: failure, ReleaseLease: true), cancellationToken);

    private Task<bool> ScheduleBlockedRetryAsync(
        FirstGskuIdentityRetirementClaim claim,
        DateTimeOffset now,
        string failure,
        CancellationToken cancellationToken) =>
        operations.AdvanceAsync(claim, new(
            FirstGskuIdentityRetirementCheckpoint.AdmissionFenceClosed,
            ProductIdentityWorkflowRecoveryDisposition.Retryable,
            now.UtcTicks,
            now.AddMinutes(5).UtcTicks,
            LastFailureCode: failure,
            ReleaseLease: true), cancellationToken);

    private Task<bool> QuarantineAsync(
        FirstGskuIdentityRetirementClaim claim,
        string? failure,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        operations.AdvanceAsync(claim, new(
            FirstGskuIdentityRetirementCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired,
            now.UtcTicks,
            LastFailureCode: Bounded(failure, "FIRST_GSKU_RETIREMENT_RECONCILIATION_REQUIRED"),
            ReleaseLease: true), cancellationToken);

    private static bool ExactFencedGsku(Gsku? value, FirstGskuIdentityRetirementOperation operation) =>
        value is not null && !value.IsDeleted && value.TenantId == operation.TenantId
        && value.Id == operation.GskuId && value.ProductDefinitionRevisionId == operation.ProductDefinitionRevisionId
        && value.Version == operation.ExpectedGskuVersion + 1
        && value.LifecycleStatus == ProductIdentityLifecycleStatus.IdentityApproved
        && value.RetirementOperationId == operation.OperationId
        && value.RetirementOperationFingerprint == operation.OperationFingerprint;

    private static bool ExactRetiredGsku(Gsku? value, FirstGskuIdentityRetirementOperation operation) =>
        value is not null && !value.IsDeleted && value.TenantId == operation.TenantId
        && value.Id == operation.GskuId && value.ProductDefinitionRevisionId == operation.ProductDefinitionRevisionId
        && value.Version == operation.ExpectedGskuVersion + 2
        && value.LifecycleStatus == ProductIdentityLifecycleStatus.Retired
        && value.RetirementOperationId == operation.OperationId
        && value.RetirementOperationFingerprint == operation.OperationFingerprint;

    private static bool ExactApprovedRevision(
        ProductDefinitionRevision? value, FirstGskuIdentityRetirementOperation operation) =>
        value is not null && !value.IsDeleted && value.TenantId == operation.TenantId
        && value.Id == operation.ProductDefinitionRevisionId
        && value.Version == operation.ExpectedRevisionVersion
        && value.LifecycleStatus == ProductIdentityLifecycleStatus.IdentityApproved;

    private static bool ExactRetiredRevision(
        ProductDefinitionRevision? value, FirstGskuIdentityRetirementOperation operation) =>
        value is not null && !value.IsDeleted && value.TenantId == operation.TenantId
        && value.Id == operation.ProductDefinitionRevisionId
        && value.Version == operation.ExpectedRevisionVersion + 1
        && value.LifecycleStatus == ProductIdentityLifecycleStatus.Retired
        && value.RetirementOperationId == operation.OperationId
        && value.RetirementOperationFingerprint == operation.OperationFingerprint;

    private static bool ExactRetirementAudit(
        Gsku gsku, ProductDefinitionRevision revision,
        FirstGskuIdentityRetirementOperation operation)
    {
        var timestamp = Timestamp(operation);
        var expectedGsku = FirstGskuIdentityRetirementAuditIntentFactory.CreateGsku(
            gsku, operation.ExpectedGskuVersion + 1, operation.OperationId,
            operation.OperationFingerprint, operation.ActorSubjectId, operation.ReasonCode, timestamp);
        var expectedRevision = FirstGskuIdentityRetirementAuditIntentFactory.CreateRevision(
            revision, operation.ExpectedRevisionVersion, operation.OperationId,
            operation.OperationFingerprint, operation.ActorSubjectId, operation.ReasonCode, timestamp);
        return gsku.AuditIntents.Count(intent => ExactImmutableAuditIntent(intent, expectedGsku)) == 1
            && revision.AuditIntents.Count(intent => ExactImmutableAuditIntent(intent, expectedRevision)) == 1;
    }

    private static bool ExactImmutableAuditIntent(
        LocalAuditIntent actual,
        LocalAuditIntent expected) =>
        actual.IntentId == expected.IntentId && actual.TenantId == expected.TenantId
        && actual.AggregateType == expected.AggregateType && actual.AggregateId == expected.AggregateId
        && actual.PreVersion == expected.PreVersion && actual.PostVersion == expected.PostVersion
        && actual.Operation == expected.Operation && actual.ActorId == expected.ActorId
        && actual.CorrelationId == expected.CorrelationId && actual.CausationId == expected.CausationId
        && actual.CommandId == expected.CommandId && actual.Sequence == expected.Sequence
        && actual.TimestampUtc == expected.TimestampUtc
        && actual.TimestampUtcTicksV1 == expected.TimestampUtcTicksV1
        && actual.TemporalStorageVersion == expected.TemporalStorageVersion
        && actual.EvidenceHash == expected.EvidenceHash
        && actual.SnapshotReference == expected.SnapshotReference
        && actual.IdempotencyKey == expected.IdempotencyKey;

    private static bool ExactPairBinding(Gsku gsku, ProductDefinitionRevision revision) =>
        gsku.IdentityWorkflowBinding is { } gskuBinding
        && revision.IdentityWorkflowBinding is { } revisionBinding
        && ExactImmutableBinding(gskuBinding, revisionBinding)
        && gskuBinding.GskuId == gsku.Id
        && gskuBinding.ProductDefinitionRevisionId == revision.Id
        && string.Equals(gskuBinding.ObjectType, FirstGskuIdentityWorkflowStartRequestFactory.ObjectType,
            StringComparison.Ordinal)
        && gskuBinding.ObjectRef.Length > 0
        && gskuBinding.SubmitterSubjectId != Guid.Empty
        && gskuBinding.TerminalDecision is { } decision
        && decision.Decision == ProductIdentityDecisionKind.Approved
        && decision.WorkflowInstanceId == gskuBinding.WorkflowInstanceId
        && decision.ApprovalTaskId == gskuBinding.ApprovalTaskId
        && decision.WorkflowTemplateId == gskuBinding.WorkflowTemplateId
        && decision.WorkflowTemplateVersionId == gskuBinding.WorkflowTemplateVersionId
        && string.Equals(decision.ObjectType, gskuBinding.ObjectType, StringComparison.Ordinal)
        && decision.ObjectId == gsku.Id
        && string.Equals(decision.ObjectRef, gskuBinding.ObjectRef, StringComparison.Ordinal)
        && decision.DecisionActorSubjectId != Guid.Empty
        && decision.DecisionActorSubjectId != gskuBinding.SubmitterSubjectId
        && decision.DecisionAtUtc.Offset == TimeSpan.Zero
        && decision.TransitionSequence > 0
        && string.Equals(decision.TaskStatus, "Approved", StringComparison.Ordinal)
        && string.Equals(decision.InstanceStatus, "Completed", StringComparison.Ordinal);

    private static bool ExactImmutableBinding(
        FirstGskuIdentityWorkflowBinding left,
        FirstGskuIdentityWorkflowBinding right) =>
        left.WorkflowInstanceId == right.WorkflowInstanceId
        && left.WorkflowTemplateId == right.WorkflowTemplateId
        && left.WorkflowTemplateVersionId == right.WorkflowTemplateVersionId
        && left.ApprovalTaskId == right.ApprovalTaskId
        && left.AssignmentSnapshotId == right.AssignmentSnapshotId
        && left.StartTransitionLogId == right.StartTransitionLogId
        && string.Equals(left.ObjectType, right.ObjectType, StringComparison.Ordinal)
        && left.GskuId == right.GskuId
        && left.ProductDefinitionRevisionId == right.ProductDefinitionRevisionId
        && string.Equals(left.ObjectRef, right.ObjectRef, StringComparison.Ordinal)
        && left.SubmitterSubjectId == right.SubmitterSubjectId
        && string.Equals(left.StartIdempotencyKey, right.StartIdempotencyKey, StringComparison.Ordinal)
        && string.Equals(left.StartRequestFingerprint, right.StartRequestFingerprint, StringComparison.Ordinal)
        && ExactTimestamp(left.SubmittedAtUtc, right.SubmittedAtUtc)
        && ExactNullableTimestamp(left.DueAtUtc, right.DueAtUtc)
        && ExactTerminalDecision(left.TerminalDecision, right.TerminalDecision);

    private static bool ExactTerminalDecision(
        ProductIdentityWorkflowDecisionEvidence? left,
        ProductIdentityWorkflowDecisionEvidence? right) =>
        left is not null && right is not null
        && left.Decision == right.Decision
        && left.WorkflowInstanceId == right.WorkflowInstanceId
        && left.ApprovalTaskId == right.ApprovalTaskId
        && left.WorkflowTemplateId == right.WorkflowTemplateId
        && left.WorkflowTemplateVersionId == right.WorkflowTemplateVersionId
        && string.Equals(left.ObjectType, right.ObjectType, StringComparison.Ordinal)
        && left.ObjectId == right.ObjectId
        && string.Equals(left.ObjectRef, right.ObjectRef, StringComparison.Ordinal)
        && left.DecisionActorSubjectId == right.DecisionActorSubjectId
        && string.Equals(left.ReasonCode, right.ReasonCode, StringComparison.Ordinal)
        && ExactTimestamp(left.DecisionAtUtc, right.DecisionAtUtc)
        && left.TransitionSequence == right.TransitionSequence
        && string.Equals(left.TaskStatus, right.TaskStatus, StringComparison.Ordinal)
        && string.Equals(left.InstanceStatus, right.InstanceStatus, StringComparison.Ordinal);

    private static bool ExactTimestamp(DateTimeOffset left, DateTimeOffset right) =>
        left.UtcTicks == right.UtcTicks && left.Offset == right.Offset;

    private static bool ExactNullableTimestamp(DateTimeOffset? left, DateTimeOffset? right) =>
        left.HasValue == right.HasValue
        && (!left.HasValue || ExactTimestamp(left.Value, right!.Value));

    private static string Fingerprint(
        Guid tenantId, Guid operationId, Guid revisionId, Guid gskuId,
        int revisionVersion, int gskuVersion, Guid actorId, string reasonCode)
    {
        var values = new[]
        {
            tenantId.ToString("D"), operationId.ToString("D"), revisionId.ToString("D"),
            gskuId.ToString("D"), revisionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            gskuVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), actorId.ToString("D"), reasonCode
        };
        var canonical = string.Concat(values.Select(value => $"{value.Length}:{value}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static DateTimeOffset Timestamp(FirstGskuIdentityRetirementOperation operation) =>
        new(operation.CreatedAtUtcTicksV1, TimeSpan.Zero);

    private static bool Exact(string? value, int max) => value is { Length: > 0 }
        && value.Length <= max && value == value.Trim() && value.All(character => !char.IsControl(character));

    private static string Bounded(string? value, string fallback) => Exact(value, 128) ? value! : fallback;

    private static GskuPairRetirementProcessingResult Success(
        FirstGskuIdentityRetirementOperation operation, bool replay) =>
        new(true, operation, null, 200, replay);

    private static GskuPairRetirementProcessingResult Fail(string code, int status) =>
        new(false, null, code, status, false);

    private static GskuPairRetirementProcessingResult Fail(
        FirstGskuIdentityRetirementOperation operation, string code, int status) =>
        new(false, operation, code, status, false);
}
