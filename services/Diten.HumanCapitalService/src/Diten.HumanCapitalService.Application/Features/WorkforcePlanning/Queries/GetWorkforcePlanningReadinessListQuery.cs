using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.WorkforcePlanning.Queries;

public sealed record GetWorkforcePlanningReadinessListQuery : IRequest<Response<IReadOnlyList<WorkforcePlanningReadinessListItemDto>>>;
