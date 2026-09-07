using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Entities;

public sealed class FirstGskuIdentityWorkflowOperation : EntityBase
{
    public const int CurrentTemporalStorageVersion = 1;

    public Guid OperationId { get; set; }
    public Guid ProductDefinitionRevisionId { get; set; }
    public Guid GskuId { get; set; }
    public Guid GlobalProductId { get; set; }
    public string CreationCommandId { get; set; } = string.Empty;
    public int ExpectedRevisionVersion { get; set; }
    public int ExpectedGskuVersion { get; set; }
    public Guid MakerSubjectId { get; set; }
    public Guid? WorkflowTemplateId { get; set; }
    public string? WorkflowTemplateCode { get; set; }
    public List<Guid> CandidatePrincipalIds { get; set; } = [];
    public string ReasonCode { get; set; } = string.Empty;
    public bool CommentRequired { get; set; }
    public bool EvidenceRequired { get; set; }
    public long? DueAtUtcTicksV1 { get; set; }
    public string ObjectType { get; set; } = string.Empty;
    public string ObjectId { get; set; } = string.Empty;
    public string ObjectRef { get; set; } = string.Empty;
    public string StartIdempotencyKey { get; set; } = string.Empty;
    public string OperationFingerprint { get; set; } = string.Empty;
    public string PackApplicabilityCode { get; set; } = string.Empty;
    public decimal PackQuantity { get; set; }
    public string PackUomCode { get; set; } = string.Empty;
    public ReferenceCatalogSelection PackApplicabilitySelection { get; set; } = new();
    public ReferenceCatalogSelection PackUomSelection { get; set; } = new();
    public ReferenceCatalogSelection? ApprovalPackApplicabilitySelection { get; set; }
    public ReferenceCatalogSelection? ApprovalPackUomSelection { get; set; }
    public long? ReferencesValidatedAtUtcTicksV1 { get; set; }
    public string? ApprovalReferenceProofFingerprint { get; set; }
    public Guid? WithdrawalCommandId { get; set; }
    public string? WithdrawalFingerprint { get; set; }
    public Guid? WithdrawalRequesterSubjectId { get; set; }
    public int? WithdrawalExpectedGskuVersion { get; set; }
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
    public FirstGskuIdentityWorkflowCheckpoint Checkpoint { get; set; }
    public ProductIdentityWorkflowRecoveryDisposition RecoveryDisposition { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? WorkflowTemplateVersionId { get; set; }
    public Guid? ApprovalTaskId { get; set; }
    public Guid? AssignmentSnapshotId { get; set; }
    public Guid? StartTransitionLogId { get; set; }
    public long? WorkflowStartedAtUtcTicksV1 { get; set; }
    public Guid? DecisionTransitionLogId { get; set; }
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
    public string? LastFailureCode { get; set; }
    public string? LeaseOwner { get; set; }
    public long? LeaseUntilUtcTicksV1 { get; set; }
    public long LeaseGeneration { get; set; }
    public Guid? RecoveryCommandId { get; set; }
    public Guid? RecoveryOperatorSubjectId { get; set; }
    public string? RecoveryReasonCode { get; set; }
    public string? RecoveryComment { get; set; }
    public Guid? RecoveryWorkflowNotFoundEvidenceId { get; set; }
    public string? RecoveryWorkflowNotFoundEvidenceFingerprint { get; set; }
    public long? RecoveryWorkflowNotFoundObservedAtUtcTicksV1 { get; set; }
    public long? RecoveredAtUtcTicksV1 { get; set; }
    public Guid? SuccessorOperationId { get; set; }
    public string? SuccessorStartIdempotencyKey { get; set; }
    public string? SuccessorOperationFingerprint { get; set; }
    public int TemporalStorageVersion { get; set; } = CurrentTemporalStorageVersion;
    public long CreatedAtUtcTicksV1 { get; set; }
    public long UpdatedAtUtcTicksV1 { get; set; }
}
