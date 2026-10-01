using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

public sealed class StartTrustedWorkflowInstanceHandler
    : IRequestHandler<StartTrustedWorkflowInstanceCommand, Response<TrustedWorkflowStartResult>>
{
    private readonly IWorkflowInstanceStartCoordinator _coordinator;
    private readonly ITrustedWorkflowStartAuthorizationPolicy _authorizationPolicy;

    public StartTrustedWorkflowInstanceHandler(
        IWorkflowInstanceStartCoordinator coordinator,
        ITrustedWorkflowStartAuthorizationPolicy authorizationPolicy)
    {
        _coordinator = coordinator;
        _authorizationPolicy = authorizationPolicy;
    }

    public Task<Response<TrustedWorkflowStartResult>> Handle(
        StartTrustedWorkflowInstanceCommand request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_authorizationPolicy.IsAuthorized(new TrustedWorkflowStartAuthorizationRequest(
                request.ServiceClientId,
                request.ServiceName,
                request.ServiceAudience,
                request.Request.ObjectType,
                request.Request.TemplateId,
                request.Request.TemplateCode)))
        {
            return Task.FromResult(Response<TrustedWorkflowStartResult>.Fail(
                "Trusted workflow start is forbidden.",
                403,
                "WORKFLOW_TRUSTED_START_FORBIDDEN",
                request.CorrelationId));
        }

        return _coordinator.StartAsync(
            request.Request,
            request.ServiceClientId,
            request.DelegatedMakerUserId,
            request.CorrelationId,
            ct);
    }
}
