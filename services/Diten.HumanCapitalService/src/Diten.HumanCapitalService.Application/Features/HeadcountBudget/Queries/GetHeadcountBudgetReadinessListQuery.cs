using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HeadcountBudget.Queries;

public sealed record GetHeadcountBudgetReadinessListQuery : IRequest<Response<IReadOnlyList<HeadcountBudgetReadinessListItemDto>>>;
