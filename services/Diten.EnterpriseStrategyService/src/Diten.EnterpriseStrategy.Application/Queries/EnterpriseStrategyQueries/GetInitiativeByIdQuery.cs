using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class GetInitiativeByIdQuery : IRequest<Response<InitiativeDetailDto>>
{
    public string InitiativeId { get; set; } = string.Empty;
}
