using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

public sealed class LskuRetirementRequestOperation : EntityBase
{
    public Guid OperationId { get; set; }
    public Guid LskuId { get; set; }
    public int BaseLskuVersion { get; set; }
    public Guid MakerSubjectId { get; set; }
    public string RequestReason { get; set; } = string.Empty;
    public Guid? WorkflowTemplateId { get; set; }
    public string? WorkflowTemplateCode { get; set; }
    public List<Guid> CandidatePrincipalIds { get; set; } = [];
    public string ReasonCode { get; set; } = string.Empty;
    public bool CommentRequired { get; set; }
    public bool EvidenceRequired { get; set; }
    public int? ConfiguredDueAfterSeconds { get; set; }
    public long? DueAtUtcTicksV1 { get; set; }
    public string ObjectType { get; set; } = "LskuRetirementRequest";
    public string ObjectId { get; set; } = string.Empty;
    public string ObjectRef { get; set; } = string.Empty;
    public string StartIdempotencyKey { get; set; } = string.Empty;
    public string OperationFingerprint { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public LskuRetirementRequestCheckpoint Checkpoint { get; set; }
    public ProductIdentityWorkflowRecoveryDisposition RecoveryDisposition { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? WorkflowTemplateVersionId { get; set; }
    public Guid? ApprovalTaskId { get; set; }
    public Guid? AssignmentSnapshotId { get; set; }
    public Guid? StartTransitionLogId { get; set; }
    public long? WorkflowStartedAtUtcTicksV1 { get; set; }
    public ProductIdentityDecisionKind? DecisionKind { get; set; }
    public Guid? DecisionActorSubjectId { get; set; }
    public string? DecisionReasonCode { get; set; }
    public long? DecisionAtUtcTicksV1 { get; set; }
    public long? DecisionTransitionSequence { get; set; }
    public string? DecisionTaskStatus { get; set; }
    public string? DecisionInstanceStatus { get; set; }
    public long? NextAttemptAtUtcTicksV1 { get; set; }
    public string? LastFailureCode { get; set; }
    public string? LeaseOwner { get; set; }
    public long? LeaseUntilUtcTicksV1 { get; set; }
    public long LeaseGeneration { get; set; }
    public long CreatedAtUtcTicksV1 { get; set; }
    public long UpdatedAtUtcTicksV1 { get; set; }
}
