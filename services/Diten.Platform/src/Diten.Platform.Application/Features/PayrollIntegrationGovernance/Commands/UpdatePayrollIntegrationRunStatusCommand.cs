using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

public sealed record UpdatePayrollIntegrationRunStatusCommand(Guid RunId, PayrollIntegrationRunStatusRequest Request) : IRequest<Response<NoContent>>;
