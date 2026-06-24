using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Commands;

public sealed record PublishWorkflowDefinitionCommand(
    Guid TemplateId,
    PublishWorkflowDefinitionRequest Request,
    string CorrelationId) : IRequest<Response<PublishWorkflowDefinitionResponse>>;
