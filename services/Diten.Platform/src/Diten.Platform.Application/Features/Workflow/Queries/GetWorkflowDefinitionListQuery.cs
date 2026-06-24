using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Queries;

public sealed record GetWorkflowDefinitionListQuery(
    string CorrelationId) : IRequest<Response<IReadOnlyList<WorkflowDefinitionListItemDto>>>;
