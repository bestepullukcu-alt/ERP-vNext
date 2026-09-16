using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.WorkforcePlanning.Commands;

public sealed record DeleteWorkforcePlanningReadinessCommand(Guid Id) : IRequest<Response<bool>>;
