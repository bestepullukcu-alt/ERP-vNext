using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HeadcountBudget.Commands;

public sealed record CreateHeadcountBudgetReadinessCommand(HeadcountBudgetReadinessCreateRequest Request) : IRequest<Response<Guid>>;
