using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Queries;

public sealed record GetPayrollIntegrationSourceLinksQuery(Guid RunId) : IRequest<Response<IReadOnlyList<PayrollIntegrationSourceLinkDto>>>;
