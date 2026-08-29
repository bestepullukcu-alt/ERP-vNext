using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Commands;

public sealed record StartTrustedWorkflowInstanceCommand(
    TrustedWorkflowStartRequest Request,
    Guid ServiceClientId,
    Guid DelegatedMakerUserId,
    string CorrelationId) : IRequest<Response<TrustedWorkflowStartResult>>;
