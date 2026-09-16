using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrKpiAnalytics.Commands;

public sealed record DeleteHrKpiAnalyticsReadinessCommand(Guid Id) : IRequest<Response<bool>>;
