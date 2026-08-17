using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

public sealed record RequestPayrollIntegrationReplayCommand(Guid RunId, PayrollIntegrationRetryReplayRequestModel Request) : IRequest<Response<Guid>>;
