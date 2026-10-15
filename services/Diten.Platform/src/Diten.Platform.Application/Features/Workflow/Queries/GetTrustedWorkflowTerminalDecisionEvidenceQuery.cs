using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Queries;

public sealed record GetTrustedWorkflowTerminalDecisionEvidenceQuery(
    Guid WorkflowInstanceId,
    Guid TrustedConsumerClientId,
    string ExpectedObjectType,
    string ExpectedObjectId,
    string CorrelationId) : IRequest<Response<TrustedWorkflowTerminalDecisionEvidence>>;
