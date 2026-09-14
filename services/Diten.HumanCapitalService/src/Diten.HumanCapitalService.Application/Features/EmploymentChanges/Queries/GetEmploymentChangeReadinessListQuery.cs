using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmploymentChanges.Queries;

public sealed record GetEmploymentChangeReadinessListQuery : IRequest<Response<IReadOnlyList<EmploymentChangeReadinessListItemDto>>>;
