using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Commands;

public sealed record CancelTrustedWorkflowInstanceCommand(
    Guid ServiceClientId,
    Guid DelegatedRequesterUserId,
    TrustedWorkflowCancellationRequest Request,
    string CorrelationId) : IRequest<Response<TrustedWorkflowCancellationEvidence>>;
