using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Queries;

public sealed record GetPayrollIntegrationMappingControlsQuery(Guid RunId) : IRequest<Response<IReadOnlyList<PayrollIntegrationMappingControlDto>>>;
