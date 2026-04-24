using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class ListInitiativeStrategyLinksQuery : IRequest<Response<PagedResponseDto<InitiativeStrategyLinkViewDto>>>
{
    public PagedRequestDto Request { get; set; } = new();
}
