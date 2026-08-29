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
