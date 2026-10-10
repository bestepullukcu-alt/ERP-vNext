using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public sealed record ItemWorkflowReserveResult(bool Succeeded, bool IsReplay, ItemWorkflowOperation? Operation, string? ErrorCode);

public sealed record ItemWorkflowClaim(
    Guid TenantId,
    Guid OperationId,
    Guid ItemId,
    string OperationFingerprint,
    string LeaseOwner,
    long LeaseGeneration,
    ItemWorkflowCheckpoint Checkpoint,
    long LeaseUntilUtcTicksV1);

public sealed record ItemWorkflowClaimRequest(
    Guid OperationId,
    string OperationFingerprint,
    IReadOnlyCollection<ItemWorkflowCheckpoint> EligibleCheckpoints,
    string LeaseOwner,
    long NowUtcTicks,
    long LeaseUntilUtcTicks);

public sealed record ItemWorkflowCheckpointMutation(
    ItemWorkflowCheckpoint NextCheckpoint,
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
    long? ApprovalGateValidatedAtUtcTicksV1 = null,
    Guid? WithdrawalCommandId = null,
    string? WithdrawalFingerprint = null,
    Guid? WithdrawalRequesterSubjectId = null,
    int? WithdrawalExpectedItemVersion = null,
    string? WithdrawalReasonCode = null,
    string? WithdrawalComment = null,
    int? WithdrawalExpectedWorkflowInstanceVersion = null,
    int? WithdrawalExpectedApprovalTaskVersion = null,
    Guid? WithdrawalTransitionLogId = null,
    long? WithdrawalObservedAtUtcTicksV1 = null,
    long? WithdrawalTransitionSequence = null,
    int? WithdrawalResultWorkflowInstanceVersion = null,
    int? WithdrawalResultApprovalTaskVersion = null,
    string? WithdrawalTaskStatus = null,
    string? WithdrawalInstanceStatus = null,
    string? WithdrawalObjectRef = null,
    bool ReleaseLease = false,
    // A withdrawal the engine refused ends — every withdrawal field goes, the approval stays.
    bool ClearWithdrawal = false,
    // The requester's repeat puts a stuck operation back before its decision — the recorded decision and gate proof go.
    bool ClearDecision = false,
    RecoveryStreakWrite Streak = RecoveryStreakWrite.Reset,
    // The start is about to be sent (kept on the same checkpoint, the lease kept).
    long? LastStartSentAtUtcTicksV1 = null,
    long? WithdrawalUnansweredSinceUtcTicksV1 = null,
    string? RetryLimitCauseCode = null);

public sealed record ItemWorkflowWriteResult(bool Succeeded, bool IsReplay, Item? Item, string? ErrorCode);

public sealed record ItemWorkflowTenantPartitionPage(IReadOnlyList<Guid> TenantIds, Guid? NextAfterTenantId);

public sealed record ItemWorkflowRecoveryCursor(long? NextAttemptAtUtcTicksV1, Guid OperationId);

public sealed record ItemWorkflowRecoverablePage(IReadOnlyList<ItemWorkflowOperation> Operations, ItemWorkflowRecoveryCursor? NextCursor);
