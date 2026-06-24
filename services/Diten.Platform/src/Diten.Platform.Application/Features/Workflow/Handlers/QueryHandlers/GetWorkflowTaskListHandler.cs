using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;

public sealed class GetWorkflowTaskListHandler
    : IRequestHandler<GetWorkflowTaskListQuery, Response<IReadOnlyList<WorkflowTaskDto>>>
{
    private readonly IApprovalTaskRepository _repository;

    public GetWorkflowTaskListHandler(IApprovalTaskRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<WorkflowTaskDto>>> Handle(GetWorkflowTaskListQuery request, CancellationToken ct)
    {
        var tasks = await _repository.GetAllForTenantAsync(ct);
        return Response<IReadOnlyList<WorkflowTaskDto>>.Success(
            tasks.Select(WorkflowDefinitionMapper.ToTask).ToList(),
            correlationId: request.CorrelationId);
    }
}
