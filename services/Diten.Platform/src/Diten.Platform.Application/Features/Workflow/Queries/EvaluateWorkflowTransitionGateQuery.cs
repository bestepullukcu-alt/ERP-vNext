using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Queries;

public sealed record EvaluateWorkflowTransitionGateQuery(
    EvaluateWorkflowTransitionGateRequest Request,
    string CorrelationId) : IRequest<Response<EvaluateWorkflowTransitionGateResponse>>;
