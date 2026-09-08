using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Application.Features.Workflow.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;

public sealed class GetTrustedWorkflowCancellationPreflightHandler
    : IRequestHandler<GetTrustedWorkflowCancellationPreflightQuery, Response<TrustedWorkflowCancellationPreflight>>
{
    private readonly ITrustedWorkflowCancellationCoordinator _coordinator;

    public GetTrustedWorkflowCancellationPreflightHandler(ITrustedWorkflowCancellationCoordinator coordinator) =>
        _coordinator = coordinator;

    public Task<Response<TrustedWorkflowCancellationPreflight>> Handle(
        GetTrustedWorkflowCancellationPreflightQuery request,
        CancellationToken ct) =>
        _coordinator.PreflightAsync(
            request.ServiceClientId,
            request.WorkflowInstanceId,
            request.ApprovalTaskId,
            request.ExpectedObjectType,
            request.ExpectedObjectId,
            request.ExpectedMakerSubjectId,
            request.CorrelationId,
            ct);
}
