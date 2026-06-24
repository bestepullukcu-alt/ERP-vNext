using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Queries;

public sealed record GetWorkflowDefinitionByIdQuery(
    Guid Id,
    string CorrelationId) : IRequest<Response<WorkflowDefinitionDetailDto>>;
