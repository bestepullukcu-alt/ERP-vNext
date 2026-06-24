using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;

public sealed class GetWorkflowInstanceListHandler
    : IRequestHandler<GetWorkflowInstanceListQuery, Response<IReadOnlyList<WorkflowInstanceDto>>>
{
    private readonly IWorkflowInstanceRepository _repository;

    public GetWorkflowInstanceListHandler(IWorkflowInstanceRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<WorkflowInstanceDto>>> Handle(
        GetWorkflowInstanceListQuery request,
        CancellationToken ct)
    {
        var instances = await _repository.GetAllForTenantAsync(ct);
        return Response<IReadOnlyList<WorkflowInstanceDto>>.Success(
            instances.Select(WorkflowDefinitionMapper.ToInstance).ToList(),
            correlationId: request.CorrelationId);
    }
}
