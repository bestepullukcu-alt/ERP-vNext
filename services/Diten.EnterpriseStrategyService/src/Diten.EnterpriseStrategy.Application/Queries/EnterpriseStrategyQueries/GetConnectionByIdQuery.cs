using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class GetConnectionByIdQuery : IRequest<Response<StrategyConnectionDto>>
{
    public string ConnectionId { get; set; } = string.Empty;
}
