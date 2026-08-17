using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Queries;

public sealed record GetPayrollIntegrationRunByIdQuery(Guid RunId) : IRequest<Response<PayrollIntegrationRunDto>>;
