namespace Diten.Platform.Application.Features.Workflow.Services;

public interface ITrustedWorkflowStartAuthorizationPolicy
{
    bool IsAuthorized(TrustedWorkflowStartAuthorizationRequest request);
}

public sealed record TrustedWorkflowStartAuthorizationRequest(
    Guid ClientId,
    string ServiceName,
    string Audience,
    string ObjectType,
    Guid? TemplateId,
    string? TemplateCode);
