using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrKpiAnalytics.Queries;

public sealed record GetHrKpiAnalyticsReadinessByIdQuery(Guid Id) : IRequest<Response<HrKpiAnalyticsReadinessDto>>;
