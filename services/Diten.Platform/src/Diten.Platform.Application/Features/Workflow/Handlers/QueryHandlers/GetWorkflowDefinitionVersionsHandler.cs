using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;

public sealed class GetWorkflowDefinitionVersionsHandler
    : IRequestHandler<GetWorkflowDefinitionVersionsQuery, Response<IReadOnlyList<WorkflowDefinitionVersionDto>>>
{
    private readonly IWorkflowTemplateRepository _templateRepository;
    private readonly IWorkflowTemplateVersionRepository _versionRepository;

    public GetWorkflowDefinitionVersionsHandler(
        IWorkflowTemplateRepository templateRepository,
        IWorkflowTemplateVersionRepository versionRepository)
    {
        _templateRepository = templateRepository;
        _versionRepository = versionRepository;
    }

    public async Task<Response<IReadOnlyList<WorkflowDefinitionVersionDto>>> Handle(
        GetWorkflowDefinitionVersionsQuery request,
        CancellationToken ct)
    {
        var template = await _templateRepository.GetByIdAsync(request.TemplateId, ct);
        if (template is null)
        {
            return Response<IReadOnlyList<WorkflowDefinitionVersionDto>>.Fail(
                "Workflow definition not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                request.CorrelationId);
        }

        var versions = await _versionRepository.ListByTemplateIdAsync(request.TemplateId, ct);
        return Response<IReadOnlyList<WorkflowDefinitionVersionDto>>.Success(
            versions.Select(WorkflowDefinitionMapper.ToVersion).ToList(),
            correlationId: request.CorrelationId);
    }
}
