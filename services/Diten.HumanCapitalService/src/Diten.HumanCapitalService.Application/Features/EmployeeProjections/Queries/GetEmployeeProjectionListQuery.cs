using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeProjections.Queries;

public sealed record GetEmployeeProjectionListQuery : IRequest<Response<IReadOnlyList<EmployeeProjectionListItemDto>>>;
