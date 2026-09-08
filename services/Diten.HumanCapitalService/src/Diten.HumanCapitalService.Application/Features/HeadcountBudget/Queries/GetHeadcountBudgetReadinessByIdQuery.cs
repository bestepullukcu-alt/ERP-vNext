using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HeadcountBudget.Queries;

public sealed record GetHeadcountBudgetReadinessByIdQuery(Guid Id) : IRequest<Response<HeadcountBudgetReadinessDto>>;
