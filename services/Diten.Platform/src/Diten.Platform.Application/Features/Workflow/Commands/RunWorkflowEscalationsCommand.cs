using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Commands;

public sealed record RunWorkflowEscalationsCommand(
    RunWorkflowEscalationsRequest Request,
    string CorrelationId) : IRequest<Response<RunWorkflowEscalationsResponse>>;
