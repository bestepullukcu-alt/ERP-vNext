using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Queries;

public sealed record GetPayrollReconciliationControlsQuery(Guid RunId) : IRequest<Response<IReadOnlyList<PayrollReconciliationControlDto>>>;
