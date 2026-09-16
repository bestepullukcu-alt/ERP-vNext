using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HeadcountBudget.Commands;

public sealed record DeleteHeadcountBudgetReadinessCommand(Guid Id) : IRequest<Response<bool>>;
