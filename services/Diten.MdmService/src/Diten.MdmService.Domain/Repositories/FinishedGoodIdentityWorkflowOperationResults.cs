using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public sealed record FinishedGoodIdentityWorkflowReserveResult(
    bool Succeeded,
    bool IsReplay,
    FinishedGoodIdentityWorkflowOperation? Operation,
    string? ErrorCode);

public sealed record FinishedGoodIdentityWorkflowClaim(
    Guid TenantId,
    Guid OperationId,
    Guid FinishedGoodId,
    Guid GskuId,
    Guid ProductDefinitionRevisionId,
    Guid? DecisionTransitionLogId,
    long? DecisionTransitionSequence,
    string OperationFingerprint,
    string LeaseOwner,
    long LeaseGeneration,
    FinishedGoodIdentityWorkflowCheckpoint Checkpoint,
    long LeaseUntilUtcTicksV1);

public sealed record FinishedGoodIdentityWorkflowClaimRequest(
    Guid OperationId,
    string OperationFingerprint,
    IReadOnlyCollection<FinishedGoodIdentityWorkflowCheckpoint> EligibleCheckpoints,
    string LeaseOwner,
    long NowUtcTicks,
    long LeaseUntilUtcTicks);

public sealed record FinishedGoodIdentityWorkflowCheckpointMutation(
    FinishedGoodIdentityWorkflowCheckpoint NextCheckpoint,
    ProductIdentityWorkflowRecoveryDisposition RecoveryDisposition,
    long UpdatedAtUtcTicks,
    long? NextAttemptAtUtcTicksV1 = null,
    string? LastFailureCode = null,
    Guid? WorkflowInstanceId = null,
    Guid? WorkflowTemplateId = null,
    Guid? WorkflowTemplateVersionId = null,
    Guid? ApprovalTaskId = null,
    Guid? AssignmentSnapshotId = null,
    Guid? StartTransitionLogId = null,
    long? WorkflowStartedAtUtcTicksV1 = null,
    Guid? DecisionTransitionLogId = null,
    ProductIdentityDecisionKind? DecisionKind = null,
    long? DecisionObservedAtUtcTicksV1 = null,
    Guid? DecisionActorSubjectId = null,
    string? DecisionReasonCode = null,
    string? DecisionObjectType = null,
    string? DecisionObjectId = null,
    string? DecisionObjectRef = null,
    Guid? DecisionWorkflowTemplateId = null,
    Guid? DecisionWorkflowTemplateVersionId = null,
    string? DecisionTaskStatus = null,
    string? DecisionInstanceStatus = null,
    long? DecisionTransitionSequence = null,
    long? DecisionAtUtcTicksV1 = null,
    long? ApprovalParentValidatedAtUtcTicksV1 = null,
    string? ApprovalParentProofFingerprint = null,
    bool ReleaseLease = false);

public sealed record FinishedGoodIdentityWorkflowTenantPartitionPage(
    IReadOnlyList<Guid> TenantIds,
    Guid? NextAfterTenantId);

public sealed record FinishedGoodIdentityWorkflowRecoveryCursor(
    long? NextAttemptAtUtcTicksV1,
    Guid OperationId);

public sealed record FinishedGoodIdentityWorkflowRecoverablePage(
    IReadOnlyList<FinishedGoodIdentityWorkflowOperation> Operations,
    FinishedGoodIdentityWorkflowRecoveryCursor? NextCursor);
