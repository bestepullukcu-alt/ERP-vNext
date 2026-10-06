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
    Guid? WithdrawalCommandId = null,
    string? WithdrawalFingerprint = null,
    Guid? WithdrawalRequesterSubjectId = null,
    int? WithdrawalExpectedLskuVersion = null,
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
    // LSKU FIX1 B5: a withdrawal the engine refused ends — every withdrawal field goes, the approval stays.
    bool ClearWithdrawal = false,
    // LSKU FIX1 B5: the requester's repeat puts a stuck operation back before its decision — the recorded decision
    // and approval proof go, the engine is read again.
    bool ClearDecision = false,
    RecoveryStreakWrite Streak = RecoveryStreakWrite.Reset,
    // LSKU FIX2 item 8: the start is about to be sent (kept on the same checkpoint, the lease is kept).
    long? LastStartSentAtUtcTicksV1 = null,
    // FIX3 item 2: when the withdrawal's cancel was first left unanswered (kept once set; cleared with the withdrawal).
    long? WithdrawalUnansweredSinceUtcTicksV1 = null);

public sealed record LskuIdentityWithdrawalWriteResult(
    bool Succeeded,
    bool IsReplay,
    Lsku? Lsku,
    string? ErrorCode);

public sealed record LskuIdentityWorkflowTenantPartitionPage(
    IReadOnlyList<Guid> TenantIds,
    Guid? NextAfterTenantId);

public sealed record LskuIdentityWorkflowRecoveryCursor(
    long? NextAttemptAtUtcTicksV1,
    Guid OperationId);

public sealed record LskuIdentityWorkflowRecoverablePage(
    IReadOnlyList<LskuIdentityWorkflowOperation> Operations,
    LskuIdentityWorkflowRecoveryCursor? NextCursor);
