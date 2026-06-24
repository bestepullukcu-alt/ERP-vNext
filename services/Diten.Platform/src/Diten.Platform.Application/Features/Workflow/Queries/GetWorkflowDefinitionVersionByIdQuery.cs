using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Queries;

public sealed record GetWorkflowDefinitionVersionByIdQuery(
    Guid TemplateId,
    Guid VersionId,
    string CorrelationId) : IRequest<Response<WorkflowDefinitionVersionDto>>;
