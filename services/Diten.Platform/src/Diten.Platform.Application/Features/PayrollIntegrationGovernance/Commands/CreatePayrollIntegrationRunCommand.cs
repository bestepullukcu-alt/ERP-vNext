using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

public sealed record CreatePayrollIntegrationRunCommand(PayrollIntegrationRunRequest Request) : IRequest<Response<Guid>>;
