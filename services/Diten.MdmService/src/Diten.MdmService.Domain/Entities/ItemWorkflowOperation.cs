using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

// MOD-0290-FU04 S2 (§3): the durable activation / reactivation request of one item (mdm_item_workflow_operations) —
// the LSKU identity operation's shape without its parent and market proof. One per (item, expected version).
public sealed class ItemWorkflowOperation : EntityBase
{
    public const int CurrentTemporalStorageVersion = 1;

    public Guid OperationId { get; set; }
    public Guid ItemId { get; set; }
    public ItemActivationKind ActivationKind { get; set; }
    public int ExpectedItemVersion { get; set; }
    public Guid MakerSubjectId { get; set; }
    public Guid? WorkflowTemplateId { get; set; }
    public string? WorkflowTemplateCode { get; set; }
    // §2.6 / GP-1b: the approver is a POSITION (the QA Manager's) — the engine resolves its holders and the first one
    // decides. The candidate sent is "position:{id}"; the id comes from configuration, never from code.
    public Guid ApproverPositionId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public bool CommentRequired { get; set; }
    public bool EvidenceRequired { get; set; }
    public long? DueAtUtcTicksV1 { get; set; }
    public string ObjectType { get; set; } = string.Empty;
    public string ObjectId { get; set; } = string.Empty;
    public string ObjectRef { get; set; } = string.Empty;
    public string StartIdempotencyKey { get; set; } = string.Empty;
    public string OperationFingerprint { get; set; } = string.Empty;
    // When the activation gate was last met at the decision (ApprovalValidated); null before.
    public long? ApprovalGateValidatedAtUtcTicksV1 { get; set; }
    public Guid? WithdrawalCommandId { get; set; }
    public string? WithdrawalFingerprint { get; set; }
    public Guid? WithdrawalRequesterSubjectId { get; set; }
    public int? WithdrawalExpectedItemVersion { get; set; }
    public string? WithdrawalReasonCode { get; set; }
    public string? WithdrawalComment { get; set; }
    public int? WithdrawalExpectedWorkflowInstanceVersion { get; set; }
    public int? WithdrawalExpectedApprovalTaskVersion { get; set; }
    public Guid? WithdrawalTransitionLogId { get; set; }
    public long? WithdrawalObservedAtUtcTicksV1 { get; set; }
    public long? WithdrawalTransitionSequence { get; set; }
    public int? WithdrawalResultWorkflowInstanceVersion { get; set; }
    public int? WithdrawalResultApprovalTaskVersion { get; set; }
    public string? WithdrawalTaskStatus { get; set; }
    public string? WithdrawalInstanceStatus { get; set; }
    public string? WithdrawalObjectRef { get; set; }
    public ItemWorkflowCheckpoint Checkpoint { get; set; }
    public ProductIdentityWorkflowRecoveryDisposition RecoveryDisposition { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? WorkflowTemplateVersionId { get; set; }
    public Guid? ApprovalTaskId { get; set; }
    public Guid? AssignmentSnapshotId { get; set; }
    public Guid? StartTransitionLogId { get; set; }
    public long? WorkflowStartedAtUtcTicksV1 { get; set; }
    public ProductIdentityDecisionKind? DecisionKind { get; set; }
    public long? DecisionObservedAtUtcTicksV1 { get; set; }
    public Guid? DecisionActorSubjectId { get; set; }
    public string? DecisionReasonCode { get; set; }
    public string? DecisionObjectType { get; set; }
    public string? DecisionObjectId { get; set; }
    public string? DecisionObjectRef { get; set; }
    public Guid? DecisionWorkflowTemplateId { get; set; }
    public Guid? DecisionWorkflowTemplateVersionId { get; set; }
    public string? DecisionTaskStatus { get; set; }
    public string? DecisionInstanceStatus { get; set; }
    public long? DecisionTransitionSequence { get; set; }
    public long? DecisionAtUtcTicksV1 { get; set; }
    public long? NextAttemptAtUtcTicksV1 { get; set; }
    // Consecutive worker attempts that ended in a counted retry (the GSKU / LSKU streak).
    public int FailedAttemptCount { get; set; }
    // Written BEFORE a start is sent. 0 (written at reservation) = tracked and NOTHING was sent — the only value that lets
    // a submit end without asking the engine. > 0 = when a start was last sent. null (a document written before the field
    // existed) = a start may have been sent: the lookup and the settle window decide.
    public long? LastStartSentAtUtcTicksV1 { get; set; }
    // When this withdrawal's cancel was first left unanswered — needing_a_person counts it past a threshold.
    public long? WithdrawalUnansweredSinceUtcTicksV1 { get; set; }
    public string? LastFailureCode { get; set; }
    // While LastFailureCode says the retry limit was reached, the cause that kept failing.
    public string? RetryLimitCauseCode { get; set; }
    public string? LeaseOwner { get; set; }
    public long? LeaseUntilUtcTicksV1 { get; set; }
    public long LeaseGeneration { get; set; }
    public int TemporalStorageVersion { get; set; } = CurrentTemporalStorageVersion;
    public long CreatedAtUtcTicksV1 { get; set; }
    public long UpdatedAtUtcTicksV1 { get; set; }
}
