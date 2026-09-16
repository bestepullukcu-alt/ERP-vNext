using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.DevelopmentPlans.Commands;

public sealed record CreateDevelopmentPlanReadinessCommand(DevelopmentPlanReadinessCreateRequest Request) : IRequest<Response<Guid>>;
