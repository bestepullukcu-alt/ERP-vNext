using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmploymentChanges.Commands;

public sealed record DeleteEmploymentChangeReadinessCommand(Guid Id) : IRequest<Response<bool>>;
