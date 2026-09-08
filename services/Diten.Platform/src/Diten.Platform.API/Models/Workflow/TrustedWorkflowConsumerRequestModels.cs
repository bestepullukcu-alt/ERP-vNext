namespace Diten.Platform.API.Models.Workflow;

public sealed record TrustedWorkflowStartTransportRequest(
    Guid? TemplateId,
    string? TemplateCode,
    string ObjectType,
    string ObjectId,
    string? ObjectRef,
    IReadOnlyList<string> CandidatePrincipalIds,
    string? ReasonCode,
    bool CommentRequired,
    bool EvidenceRequired,
    DateTimeOffset? DueAt);

public sealed record TrustedWorkflowTerminalDecisionEvidenceTransportRequest(
    Guid WorkflowInstanceId,
    string ExpectedObjectType,
    string ExpectedObjectId);

public sealed record TrustedWorkflowStartResultTransportRequest(
    string ExpectedObjectType,
    string ExpectedObjectId,
    Guid ExpectedMakerSubjectId);

public sealed record TrustedWorkflowCancellationPreflightTransportRequest(
    Guid WorkflowInstanceId,
    Guid ApprovalTaskId,
    string ExpectedObjectType,
    string ExpectedObjectId,
    Guid ExpectedMakerSubjectId);

public sealed record TrustedWorkflowCancellationTransportRequest(
    Guid WorkflowInstanceId,
    Guid ApprovalTaskId,
    string ExpectedObjectType,
    string ExpectedObjectId,
    Guid ExpectedMakerSubjectId,
    int ExpectedWorkflowInstanceVersion,
    int ExpectedApprovalTaskVersion,
    string ReasonCode,
    string? Comment);
