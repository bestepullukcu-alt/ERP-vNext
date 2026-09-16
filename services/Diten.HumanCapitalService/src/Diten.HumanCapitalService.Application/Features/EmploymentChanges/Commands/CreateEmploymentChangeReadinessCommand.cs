using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmploymentChanges.Commands;

public sealed record CreateEmploymentChangeReadinessCommand(EmploymentChangeReadinessCreateRequest Request) : IRequest<Response<Guid>>;
