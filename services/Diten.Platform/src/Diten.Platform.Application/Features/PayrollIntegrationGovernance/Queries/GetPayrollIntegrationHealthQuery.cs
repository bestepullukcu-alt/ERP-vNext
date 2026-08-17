using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Queries;

public sealed record GetPayrollIntegrationHealthQuery(Guid? RunId) : IRequest<Response<PayrollIntegrationHealthSnapshotDto>>;
