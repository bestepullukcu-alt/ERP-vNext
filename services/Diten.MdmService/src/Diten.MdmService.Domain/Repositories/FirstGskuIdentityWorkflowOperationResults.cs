using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Repositories;

public sealed record FirstGskuIdentityWorkflowReserveResult(
    bool Succeeded, bool IsReplay, FirstGskuIdentityWorkflowOperation? Operation, string? ErrorCode);

public sealed record FirstGskuIdentityWorkflowClaim(
    Guid TenantId, Guid OperationId, string OperationFingerprint, string LeaseOwner,
    long LeaseGeneration, FirstGskuIdentityWorkflowCheckpoint Checkpoint, long LeaseUntilUtcTicksV1);

public sealed record FirstGskuIdentityWorkflowClaimRequest(
    Guid OperationId, string OperationFingerprint,
    IReadOnlyCollection<FirstGskuIdentityWorkflowCheckpoint> EligibleCheckpoints,
    string LeaseOwner, long NowUtcTicks, long LeaseUntilUtcTicks);

public sealed record FirstGskuIdentityWorkflowCheckpointMutation(
    FirstGskuIdentityWorkflowCheckpoint NextCheckpoint,
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
    ReferenceCatalogSelection? ApprovalPackApplicabilitySelection = null,
    ReferenceCatalogSelection? ApprovalPackUomSelection = null,
    long? ReferencesValidatedAtUtcTicksV1 = null,
    string? ApprovalReferenceProofFingerprint = null,
    bool ReleaseLease = false);

public sealed record FirstGskuIdentityWorkflowRecoveryCursor(long? NextAttemptAtUtcTicksV1, Guid OperationId);
public sealed record FirstGskuIdentityWorkflowRecoverablePage(
    IReadOnlyList<FirstGskuIdentityWorkflowOperation> Operations,
    FirstGskuIdentityWorkflowRecoveryCursor? NextCursor);
public sealed record FirstGskuIdentityWorkflowTenantPartitionPage(
    IReadOnlyList<Guid> TenantIds, Guid? NextAfterTenantId);

public sealed record FirstGskuIdentityLifecycleMutationResult<T>(
    bool Succeeded, bool IsReplay, T? Aggregate, string? ErrorCode) where T : EntityBase;
