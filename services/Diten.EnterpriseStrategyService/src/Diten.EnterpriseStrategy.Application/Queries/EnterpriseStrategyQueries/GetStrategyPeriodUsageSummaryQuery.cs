using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class GetStrategyPeriodUsageSummaryQuery : IRequest<Response<StrategyPeriodUsageSummaryDto>>
{
    public string StrategyPeriodId { get; set; } = string.Empty;
}
