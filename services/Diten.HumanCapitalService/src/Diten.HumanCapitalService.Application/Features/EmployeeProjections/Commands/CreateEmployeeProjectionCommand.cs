using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeProjections.Commands;

public sealed record CreateEmployeeProjectionCommand(EmployeeProjectionCreateRequest Request) : IRequest<Response<Guid>>;
