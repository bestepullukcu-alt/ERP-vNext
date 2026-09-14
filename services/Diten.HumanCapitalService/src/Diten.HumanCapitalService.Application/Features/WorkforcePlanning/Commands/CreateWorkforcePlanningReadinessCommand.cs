using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.WorkforcePlanning.Commands;

public sealed record CreateWorkforcePlanningReadinessCommand(WorkforcePlanningReadinessCreateRequest Request) : IRequest<Response<Guid>>;
