using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

public sealed record CreatePayrollIntegrationExceptionCommand(Guid RunId, PayrollIntegrationExceptionRequest Request) : IRequest<Response<Guid>>;
