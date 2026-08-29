using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.ValueObjects;

public sealed class ProductIdentityWorkflowBinding
{
    public Guid WorkflowInstanceId { get; set; }
    public Guid WorkflowTemplateId { get; set; }
    public Guid WorkflowTemplateVersionId { get; set; }
    public Guid ApprovalTaskId { get; set; }
    public Guid AssignmentSnapshotId { get; set; }
    public Guid StartTransitionLogId { get; set; }
    public string ObjectType { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public string ObjectRef { get; set; } = string.Empty;
    public Guid SubmitterSubjectId { get; set; }
    public string StartIdempotencyKey { get; set; } = string.Empty;
    public string StartRequestFingerprint { get; set; } = string.Empty;
    public DateTimeOffset SubmittedAtUtc { get; set; }
    public DateTimeOffset? DueAtUtc { get; set; }
    public ProductIdentityWorkflowDecisionEvidence? TerminalDecision { get; set; }
}

public sealed class ProductIdentityWorkflowDecisionEvidence
{
    public ProductIdentityDecisionKind Decision { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public Guid ApprovalTaskId { get; set; }
    public Guid WorkflowTemplateId { get; set; }
    public Guid WorkflowTemplateVersionId { get; set; }
    public string ObjectType { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public string ObjectRef { get; set; } = string.Empty;
    public Guid DecisionActorSubjectId { get; set; }
    public string? ReasonCode { get; set; }
    public DateTimeOffset DecisionAtUtc { get; set; }
    public long TransitionSequence { get; set; }
    public string TaskStatus { get; set; } = string.Empty;
    public string InstanceStatus { get; set; } = string.Empty;
}
