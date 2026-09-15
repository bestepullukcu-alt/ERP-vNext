using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmploymentChanges.Queries;

public sealed record GetEmploymentChangeReadinessByIdQuery(Guid Id) : IRequest<Response<EmploymentChangeReadinessDto>>;
