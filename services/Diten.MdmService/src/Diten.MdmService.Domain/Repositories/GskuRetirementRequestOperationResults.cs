using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public sealed record GskuRetirementRequestWorkflowReserveResult(
    bool Succeeded, bool IsReplay, GskuRetirementRequestOperation? Operation, string? ErrorCode);

public sealed record GskuRetirementRequestWorkflowClaim(
    Guid TenantId, Guid OperationId, string OperationFingerprint, string LeaseOwner,
    long LeaseGeneration, GskuRetirementRequestCheckpoint Checkpoint);

public sealed record GskuRetirementRequestWorkflowMutation(
    GskuRetirementRequestCheckpoint NextCheckpoint,
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

public sealed record GskuRetirementRequestWorkflowRecoverablePage(
    IReadOnlyList<GskuRetirementRequestOperation> Operations,
    Guid? NextAfterOperationId);

public sealed record GskuRetirementRequestWriteResult(
    bool Succeeded, bool IsReplay, Gsku? Gsku, string? ErrorCode = null);
