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
    string LeaseOwner, long NowUtcTicks, long LeaseUntilUtcTicks,
    // A requester who is present is not paced by the worker's retry schedule (never by a held lease).
    bool IgnoreRetrySchedule = false);

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
    Guid? WithdrawalCommandId = null,
    string? WithdrawalFingerprint = null,
    Guid? WithdrawalRequesterSubjectId = null,
    int? WithdrawalExpectedGskuVersion = null,
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
    // null leaves the two service-fault fields as they are; a value writes BOTH, exactly as given.
    int? ServiceFaultCount = null,
    long? ServiceFaultSinceUtcTicksV1 = null,
    // Forgets a withdrawal that did not happen (every Withdrawal* field), so the requester can ask again.
    bool ClearWithdrawal = false,
    string? WithdrawalRefusalCode = null,
    long? WithdrawalAwaitingRequesterSinceUtcTicksV1 = null,
    string? ApprovalAppliedNote = null);

public sealed record FirstGskuIdentityDecisionNotApplicableWriteResult(
    bool Succeeded,
    bool IsReplay,
    ProductDefinitionRevision? Revision,
    Gsku? Gsku,
    string? ErrorCode);

public sealed record FirstGskuIdentityWithdrawalWriteResult(
    bool Succeeded,
    bool IsReplay,
    ProductDefinitionRevision? Revision,
    Gsku? Gsku,
    string? ErrorCode);

public sealed record FirstGskuIdentityWorkflowRecoveryCursor(long? NextAttemptAtUtcTicksV1, Guid OperationId);
public sealed record FirstGskuIdentityWorkflowRecoverablePage(
    IReadOnlyList<FirstGskuIdentityWorkflowOperation> Operations,
    FirstGskuIdentityWorkflowRecoveryCursor? NextCursor);
public sealed record FirstGskuIdentityWorkflowTenantPartitionPage(
    IReadOnlyList<Guid> TenantIds, Guid? NextAfterTenantId);

public sealed record FirstGskuIdentityLifecycleMutationResult<T>(
    bool Succeeded, bool IsReplay, T? Aggregate, string? ErrorCode) where T : EntityBase;

public static class FirstGskuIdentityDecisionNotApplicable
{
    // The one key both audit intents and the replay check use for an operation's "could not apply" write.
    public static string Key(Guid operationId) => $"not-applicable:{operationId:D}";
}
