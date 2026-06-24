using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Commands;

public sealed record StartWorkflowInstanceCommand(
    StartWorkflowInstanceRequest Request,
    string CorrelationId) : IRequest<Response<StartWorkflowInstanceResponse>>;
