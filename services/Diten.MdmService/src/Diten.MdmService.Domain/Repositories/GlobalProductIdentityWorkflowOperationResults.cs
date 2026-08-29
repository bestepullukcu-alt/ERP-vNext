using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public sealed record GlobalProductIdentityWorkflowReserveResult(
    bool Succeeded,
    bool IsReplay,
    GlobalProductIdentityWorkflowOperation? Operation,
    string? ErrorCode);

public sealed record GlobalProductIdentityWorkflowClaim(
    Guid TenantId,
    Guid OperationId,
    string OperationFingerprint,
    string LeaseOwner,
    long LeaseGeneration,
    GlobalProductIdentityWorkflowCheckpoint Checkpoint,
    long LeaseUntilUtcTicksV1);

public sealed record GlobalProductIdentityWorkflowClaimRequest(
    Guid OperationId,
    string OperationFingerprint,
    IReadOnlyCollection<GlobalProductIdentityWorkflowCheckpoint> EligibleCheckpoints,
    string LeaseOwner,
    long NowUtcTicks,
    long LeaseUntilUtcTicks);

public sealed record GlobalProductIdentityWorkflowCheckpointMutation(
    GlobalProductIdentityWorkflowCheckpoint NextCheckpoint,
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
    bool ReleaseLease = false);

public sealed record GlobalProductIdentityWorkflowTenantPartitionPage(
    IReadOnlyList<Guid> TenantIds,
    Guid? NextAfterTenantId);

public sealed record GlobalProductIdentityWorkflowRecoveryCursor(
    long? NextAttemptAtUtcTicksV1,
    Guid OperationId);

public sealed record GlobalProductIdentityWorkflowRecoverablePage(
    IReadOnlyList<GlobalProductIdentityWorkflowOperation> Operations,
    GlobalProductIdentityWorkflowRecoveryCursor? NextCursor);
