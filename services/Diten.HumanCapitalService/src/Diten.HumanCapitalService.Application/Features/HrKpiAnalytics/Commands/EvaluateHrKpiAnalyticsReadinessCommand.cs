using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrKpiAnalytics.Commands;

public sealed record EvaluateHrKpiAnalyticsReadinessCommand(Guid Id) : IRequest<Response<HrKpiAnalyticsReadinessDto>>;
