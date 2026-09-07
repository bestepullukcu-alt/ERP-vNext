using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public sealed record GlobalProductRetirementRequestReserveResult(bool Succeeded, bool IsReplay,
    GlobalProductRetirementRequestOperation? Operation, string? ErrorCode);
public sealed record GlobalProductRetirementRequestClaim(Guid TenantId, Guid OperationId,
    string OperationFingerprint, string LeaseOwner, long LeaseGeneration,
    GlobalProductRetirementRequestCheckpoint Checkpoint);
public sealed record GlobalProductRetirementRequestMutation(
    GlobalProductRetirementRequestCheckpoint NextCheckpoint,
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
    ProductIdentityDecisionKind? DecisionKind = null,
    Guid? DecisionActorSubjectId = null,
    string? DecisionReasonCode = null,
    long? DecisionAtUtcTicksV1 = null,
    long? DecisionTransitionSequence = null,
    string? DecisionTaskStatus = null,
    string? DecisionInstanceStatus = null,
    bool ReleaseLease = false);
public sealed record GlobalProductRetirementRequestRecoverablePage(
    IReadOnlyList<GlobalProductRetirementRequestOperation> Operations, Guid? NextAfterOperationId);
