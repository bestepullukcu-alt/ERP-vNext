using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

public sealed class CancelTrustedWorkflowInstanceHandler
    : IRequestHandler<CancelTrustedWorkflowInstanceCommand, Response<TrustedWorkflowCancellationEvidence>>
{
    private readonly ITrustedWorkflowCancellationCoordinator _coordinator;

    public CancelTrustedWorkflowInstanceHandler(ITrustedWorkflowCancellationCoordinator coordinator) =>
        _coordinator = coordinator;

    public Task<Response<TrustedWorkflowCancellationEvidence>> Handle(
        CancelTrustedWorkflowInstanceCommand request,
        CancellationToken ct) =>
        _coordinator.CancelAsync(
            request.ServiceClientId,
            request.DelegatedRequesterUserId,
            request.Request,
            request.CorrelationId,
            ct);
}
