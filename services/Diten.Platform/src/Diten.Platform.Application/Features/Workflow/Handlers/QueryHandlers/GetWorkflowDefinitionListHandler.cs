using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;

public sealed class GetWorkflowDefinitionListHandler
    : IRequestHandler<GetWorkflowDefinitionListQuery, Response<IReadOnlyList<WorkflowDefinitionListItemDto>>>
{
    private readonly IWorkflowTemplateRepository _repository;

    public GetWorkflowDefinitionListHandler(IWorkflowTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<WorkflowDefinitionListItemDto>>> Handle(
        GetWorkflowDefinitionListQuery request,
        CancellationToken ct)
    {
        // GetAllForTenantAsync is tenant-scoped and excludes soft-deleted rows (TenantRepository
        // execution filter), so the list contains only the current tenant's active definitions.
        var templates = await _repository.GetAllForTenantAsync(ct);
        var items = templates.Select(WorkflowDefinitionMapper.ToListItem).ToList();

        return Response<IReadOnlyList<WorkflowDefinitionListItemDto>>.Success(
            items,
            correlationId: request.CorrelationId);
    }
}
