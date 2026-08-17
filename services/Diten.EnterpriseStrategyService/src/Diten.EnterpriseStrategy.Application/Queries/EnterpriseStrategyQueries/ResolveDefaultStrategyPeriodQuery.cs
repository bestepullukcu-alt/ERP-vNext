using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class ResolveDefaultStrategyPeriodQuery : IRequest<Response<StrategyPeriodDto>>
{
    public string CompanyId { get; set; } = string.Empty;
    public string? BusinessUnitId { get; set; }
    public string? RegionId { get; set; }
}
