using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeProjections.Commands;

public sealed record UpdateEmployeeProjectionCommand(Guid Id, EmployeeProjectionUpdateRequest Request) : IRequest<Response<EmployeeProjectionDto>>;
