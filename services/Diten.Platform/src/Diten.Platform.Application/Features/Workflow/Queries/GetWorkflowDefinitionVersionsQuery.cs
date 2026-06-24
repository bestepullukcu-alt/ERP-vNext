using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Queries;

public sealed record GetWorkflowDefinitionVersionsQuery(
    Guid TemplateId,
    string CorrelationId) : IRequest<Response<IReadOnlyList<WorkflowDefinitionVersionDto>>>;
