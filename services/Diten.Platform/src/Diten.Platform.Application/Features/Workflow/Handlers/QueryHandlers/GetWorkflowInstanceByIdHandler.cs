using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;

public sealed class GetWorkflowInstanceByIdHandler
    : IRequestHandler<GetWorkflowInstanceByIdQuery, Response<WorkflowInstanceDto>>
{
    private readonly IWorkflowInstanceRepository _repository;

    public GetWorkflowInstanceByIdHandler(IWorkflowInstanceRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<WorkflowInstanceDto>> Handle(GetWorkflowInstanceByIdQuery request, CancellationToken ct)
    {
        var instance = await _repository.GetByIdAsync(request.Id, ct);
        if (instance is null)
        {
            return Response<WorkflowInstanceDto>.Fail(
                "Workflow instance not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                request.CorrelationId);
        }

        return Response<WorkflowInstanceDto>.Success(
            WorkflowDefinitionMapper.ToInstance(instance),
            correlationId: request.CorrelationId);
    }
}
