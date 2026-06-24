using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;

public sealed class GetWorkflowDefinitionByIdHandler
    : IRequestHandler<GetWorkflowDefinitionByIdQuery, Response<WorkflowDefinitionDetailDto>>
{
    private readonly IWorkflowTemplateRepository _repository;

    public GetWorkflowDefinitionByIdHandler(IWorkflowTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<WorkflowDefinitionDetailDto>> Handle(
        GetWorkflowDefinitionByIdQuery request,
        CancellationToken ct)
    {
        // GetByIdAsync is tenant-scoped: a cross-tenant id resolves to null here, so the response is an
        // identical 404 NOT_FOUND_NON_LEAKAGE with no leaked metadata.
        var template = await _repository.GetByIdAsync(request.Id, ct);
        if (template is null)
        {
            return Response<WorkflowDefinitionDetailDto>.Fail(
                "Workflow definition not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                request.CorrelationId);
        }

        return Response<WorkflowDefinitionDetailDto>.Success(
            WorkflowDefinitionMapper.ToDetail(template),
            correlationId: request.CorrelationId);
    }
}
