using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Repositories;

public sealed record LskuIdentityWorkflowReserveResult(
    bool Succeeded,
    bool IsReplay,
    LskuIdentityWorkflowOperation? Operation,
    string? ErrorCode);

public sealed record LskuIdentityWorkflowClaim(
    Guid TenantId,
    Guid OperationId,
    Guid LskuId,
    string MarketCode,
    Guid? DecisionTransitionLogId,
    long? DecisionTransitionSequence,
    string OperationFingerprint,
    string LeaseOwner,
    long LeaseGeneration,
    LskuIdentityWorkflowCheckpoint Checkpoint,
    long LeaseUntilUtcTicksV1);

public sealed record LskuIdentityWorkflowClaimRequest(
    Guid OperationId,
    string OperationFingerprint,
    IReadOnlyCollection<LskuIdentityWorkflowCheckpoint> EligibleCheckpoints,
    string LeaseOwner,
    long NowUtcTicks,
    long LeaseUntilUtcTicks);

public sealed record LskuIdentityWorkflowCheckpointMutation(
    LskuIdentityWorkflowCheckpoint NextCheckpoint,
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
    ReferenceCatalogSelection? ApprovalMarketSelection = null,
    long? MarketValidatedAtUtcTicksV1 = null,
    string? ApprovalMarketProofFingerprint = null,
    bool ReleaseLease = false);

public sealed record LskuIdentityWorkflowTenantPartitionPage(
    IReadOnlyList<Guid> TenantIds,
    Guid? NextAfterTenantId);

public sealed record LskuIdentityWorkflowRecoveryCursor(
    long? NextAttemptAtUtcTicksV1,
    Guid OperationId);

public sealed record LskuIdentityWorkflowRecoverablePage(
    IReadOnlyList<LskuIdentityWorkflowOperation> Operations,
    LskuIdentityWorkflowRecoveryCursor? NextCursor);
