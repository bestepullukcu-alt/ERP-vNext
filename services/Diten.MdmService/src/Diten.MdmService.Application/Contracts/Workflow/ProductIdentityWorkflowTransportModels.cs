namespace Diten.MdmService.Application.Contracts.Workflow;

public enum ProductIdentityWorkflowTransportOutcome
{
    Success,
    NotFound,
    Incomplete,
    NonTerminal,
    Conflict,
    AuthenticationRejected,
    Forbidden,
    Invalid,
    Retryable,
    Timeout
}

public sealed record ProductIdentityWorkflowTransportResult<T>(
    ProductIdentityWorkflowTransportOutcome Outcome,
    T? Value,
    string? ErrorCode)
{
    public static ProductIdentityWorkflowTransportResult<T> Success(T value) =>
        new(ProductIdentityWorkflowTransportOutcome.Success, value, null);
    public static ProductIdentityWorkflowTransportResult<T> Fail(
        ProductIdentityWorkflowTransportOutcome outcome,
        string errorCode) => new(outcome, default, errorCode);
}

public sealed record ProductIdentityWorkflowStartRequest(
    Guid? TemplateId,
    string? TemplateCode,
    string ObjectType,
    string ObjectId,
    string? ObjectRef,
    IReadOnlyList<string> CandidatePrincipalIds,
    string? ReasonCode,
    string IdempotencyKey,
    bool CommentRequired,
    bool EvidenceRequired,
    DateTimeOffset? DueAt);

public sealed record ProductIdentityWorkflowStartResultRequest(
    string ExpectedObjectType,
    string ExpectedObjectId,
    Guid ExpectedMakerSubjectId,
    string IdempotencyKey);

public sealed record ProductIdentityWorkflowTerminalEvidenceRequest(
    Guid WorkflowInstanceId,
    string ExpectedObjectType,
    string ExpectedObjectId);

public sealed record ProductIdentityWorkflowCancellationPreflightRequest(
    Guid WorkflowInstanceId,
    Guid ApprovalTaskId,
    string ExpectedObjectType,
    string ExpectedObjectId,
    Guid ExpectedMakerSubjectId);

public sealed record ProductIdentityWorkflowCancellationRequest(
    Guid WorkflowInstanceId,
    Guid ApprovalTaskId,
    string ExpectedObjectType,
    string ExpectedObjectId,
    Guid ExpectedMakerSubjectId,
    int ExpectedWorkflowInstanceVersion,
    int ExpectedApprovalTaskVersion,
    string ReasonCode,
    string? Comment,
    string IdempotencyKey);

public sealed record ProductIdentityWorkflowStartResult(
    Guid WorkflowInstanceId,
    Guid TemplateId,
    Guid TemplateVersionId,
    Guid ApprovalTaskId,
    Guid AssignmentSnapshotId,
    Guid StartTransitionLogId,
    string ObjectRef,
    string Status,
    string CurrentStage,
    string CurrentStep,
    DateTimeOffset? StartedAt,
    DateTimeOffset? DueAt,
    bool IsReplay,
    string? CorrelationId);

public sealed record ProductIdentityWorkflowTerminalEvidence(
    Guid WorkflowInstanceId,
    Guid ApprovalTaskId,
    Guid TemplateId,
    Guid TemplateVersionId,
    string ObjectType,
    string ObjectId,
    string ObjectRef,
    string TerminalAction,
    string ActorUserId,
    string? ReasonCode,
    DateTimeOffset DecisionAt,
    long TransitionSequence,
    string TaskStatus,
    string InstanceStatus,
    string? CorrelationId);

public sealed record ProductIdentityWorkflowCancellationPreflight(
    Guid WorkflowInstanceId,
    Guid ApprovalTaskId,
    string ObjectType,
    string ObjectId,
    string ObjectRef,
    int WorkflowInstanceVersion,
    int ApprovalTaskVersion,
    string WorkflowInstanceStatus,
    string ApprovalTaskStatus);

public sealed record ProductIdentityWorkflowCancellationEvidence(
    Guid WorkflowInstanceId,
    Guid ApprovalTaskId,
    Guid TemplateId,
    Guid TemplateVersionId,
    string ObjectType,
    string ObjectId,
    string ObjectRef,
    string TerminalAction,
    Guid ActorUserId,
    string ReasonCode,
    string? Comment,
    DateTimeOffset DecisionAt,
    long TransitionSequence,
    Guid TransitionLogId,
    string TaskStatus,
    string InstanceStatus,
    int WorkflowInstanceVersion,
    int ApprovalTaskVersion,
    bool IsReplay,
    string? CorrelationId);
