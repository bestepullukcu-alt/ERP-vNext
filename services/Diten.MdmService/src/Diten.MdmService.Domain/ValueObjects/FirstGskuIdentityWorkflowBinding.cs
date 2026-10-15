using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.ValueObjects;

public sealed class FirstGskuIdentityWorkflowBinding
{
    public Guid WorkflowInstanceId { get; set; }
    public Guid WorkflowTemplateId { get; set; }
    public Guid WorkflowTemplateVersionId { get; set; }
    public Guid ApprovalTaskId { get; set; }
    public Guid AssignmentSnapshotId { get; set; }
    public Guid StartTransitionLogId { get; set; }
    public string ObjectType { get; set; } = string.Empty;
    public Guid GskuId { get; set; }
    public Guid ProductDefinitionRevisionId { get; set; }
    public string ObjectRef { get; set; } = string.Empty;
    public Guid SubmitterSubjectId { get; set; }
    public string StartIdempotencyKey { get; set; } = string.Empty;
    public string StartRequestFingerprint { get; set; } = string.Empty;
    public DateTimeOffset SubmittedAtUtc { get; set; }
    public DateTimeOffset? DueAtUtc { get; set; }
    public ProductIdentityWorkflowDecisionEvidence? TerminalDecision { get; set; }
}
