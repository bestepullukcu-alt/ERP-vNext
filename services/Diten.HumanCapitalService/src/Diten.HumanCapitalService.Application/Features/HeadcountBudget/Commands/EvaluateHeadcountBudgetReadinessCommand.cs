using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HeadcountBudget.Commands;

public sealed record EvaluateHeadcountBudgetReadinessCommand(Guid Id) : IRequest<Response<HeadcountBudgetReadinessDto>>;
