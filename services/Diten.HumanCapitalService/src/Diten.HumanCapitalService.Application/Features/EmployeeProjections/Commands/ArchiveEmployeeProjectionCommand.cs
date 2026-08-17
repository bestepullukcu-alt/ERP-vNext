using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeProjections.Commands;

public sealed record ArchiveEmployeeProjectionCommand(Guid Id) : IRequest<Response<NoContent>>;
