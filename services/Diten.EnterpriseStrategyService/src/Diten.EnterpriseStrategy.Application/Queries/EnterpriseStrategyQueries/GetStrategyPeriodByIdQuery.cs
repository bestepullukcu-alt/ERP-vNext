using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class GetStrategyPeriodByIdQuery : IRequest<Response<StrategyPeriodDto>>
{
    public string StrategyPeriodId { get; set; } = string.Empty;
}
