using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

public sealed record UpdatePayrollIntegrationMappingControlCommand(Guid RunId, Guid ControlId, PayrollIntegrationMappingControlRequest Request) : IRequest<Response<NoContent>>;
