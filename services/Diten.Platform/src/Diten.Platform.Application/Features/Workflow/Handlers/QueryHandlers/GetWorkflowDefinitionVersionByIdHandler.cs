using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;

public sealed class GetWorkflowDefinitionVersionByIdHandler
    : IRequestHandler<GetWorkflowDefinitionVersionByIdQuery, Response<WorkflowDefinitionVersionDto>>
{
    private readonly IWorkflowTemplateRepository _templateRepository;
    private readonly IWorkflowTemplateVersionRepository _versionRepository;

    public GetWorkflowDefinitionVersionByIdHandler(
        IWorkflowTemplateRepository templateRepository,
        IWorkflowTemplateVersionRepository versionRepository)
    {
        _templateRepository = templateRepository;
        _versionRepository = versionRepository;
    }

    public async Task<Response<WorkflowDefinitionVersionDto>> Handle(
        GetWorkflowDefinitionVersionByIdQuery request,
        CancellationToken ct)
    {
        var template = await _templateRepository.GetByIdAsync(request.TemplateId, ct);
        if (template is null)
        {
            return Response<WorkflowDefinitionVersionDto>.Fail(
                "Workflow definition not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                request.CorrelationId);
        }

        var version = await _versionRepository.GetByIdForTemplateAsync(request.TemplateId, request.VersionId, ct);
        if (version is null)
        {
            return Response<WorkflowDefinitionVersionDto>.Fail(
                "Workflow definition version not found.",
                404,
                WorkflowReasonCodes.WorkflowTemplateVersionNotFound,
                request.CorrelationId);
        }

        return Response<WorkflowDefinitionVersionDto>.Success(
            WorkflowDefinitionMapper.ToVersion(version),
            correlationId: request.CorrelationId);
    }
}
