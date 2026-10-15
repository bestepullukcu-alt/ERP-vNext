using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Commands;

public sealed record StartTrustedWorkflowInstanceCommand(
    TrustedWorkflowStartRequest Request,
    Guid ServiceClientId,
    string ServiceName,
    string ServiceAudience,
    Guid DelegatedMakerUserId,
    string CorrelationId) : IRequest<Response<TrustedWorkflowStartResult>>;
