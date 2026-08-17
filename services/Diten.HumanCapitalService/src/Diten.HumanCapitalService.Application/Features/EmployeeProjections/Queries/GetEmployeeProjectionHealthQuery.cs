using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeProjections.Queries;

public sealed record EmployeeProjectionHealthDto(string RuntimeOwnerKey, string Status);

public sealed record GetEmployeeProjectionHealthQuery : IRequest<Response<EmployeeProjectionHealthDto>>;
