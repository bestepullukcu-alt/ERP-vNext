using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Commands;

public sealed record CreateWorkflowDefinitionCommand(
    CreateWorkflowDefinitionRequest Request,
    string CorrelationId) : IRequest<Response<WorkflowDefinitionDetailDto>>;
