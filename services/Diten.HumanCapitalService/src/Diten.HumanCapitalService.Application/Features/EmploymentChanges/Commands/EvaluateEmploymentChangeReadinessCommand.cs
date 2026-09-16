using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmploymentChanges.Commands;

public sealed record EvaluateEmploymentChangeReadinessCommand(Guid Id) : IRequest<Response<EmploymentChangeReadinessDto>>;
