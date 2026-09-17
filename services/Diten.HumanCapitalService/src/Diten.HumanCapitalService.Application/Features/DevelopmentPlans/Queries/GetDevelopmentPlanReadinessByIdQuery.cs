using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.DevelopmentPlans.Queries;

public sealed record GetDevelopmentPlanReadinessByIdQuery(Guid Id) : IRequest<Response<DevelopmentPlanReadinessDto>>;
