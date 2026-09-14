using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.DevelopmentPlans.Commands;

public sealed record EvaluateDevelopmentPlanReadinessCommand(Guid Id) : IRequest<Response<DevelopmentPlanReadinessDto>>;
