using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.WorkforcePlanning.Commands;

public sealed record EvaluateWorkforcePlanningReadinessCommand(Guid Id) : IRequest<Response<WorkforcePlanningReadinessDto>>;
