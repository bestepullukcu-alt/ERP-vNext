using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class ListConnectionsQuery : IRequest<Response<PagedResponseDto<StrategyConnectionDto>>>
{
    public PagedRequestDto Request { get; set; } = new();
}
