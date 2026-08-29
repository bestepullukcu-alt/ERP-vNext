using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

public sealed class StartTrustedWorkflowInstanceHandler
    : IRequestHandler<StartTrustedWorkflowInstanceCommand, Response<TrustedWorkflowStartResult>>
{
    private readonly IWorkflowInstanceStartCoordinator _coordinator;

    public StartTrustedWorkflowInstanceHandler(IWorkflowInstanceStartCoordinator coordinator) =>
        _coordinator = coordinator;

    public Task<Response<TrustedWorkflowStartResult>> Handle(
        StartTrustedWorkflowInstanceCommand request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _coordinator.StartAsync(
            request.Request,
            request.ServiceClientId,
            request.DelegatedMakerUserId,
            request.CorrelationId,
            ct);
    }
}
