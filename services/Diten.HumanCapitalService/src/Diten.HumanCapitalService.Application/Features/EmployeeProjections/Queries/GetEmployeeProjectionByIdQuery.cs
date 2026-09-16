using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeProjections.Queries;

public sealed record GetEmployeeProjectionByIdQuery(Guid Id) : IRequest<Response<EmployeeProjectionDto>>;
