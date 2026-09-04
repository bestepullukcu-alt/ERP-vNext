using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed class ListPortfoliosHandler(PortfolioService service) : IRequestHandler<ListPortfoliosQuery, Response<IReadOnlyList<PortfolioDto>>>
{
    public Task<Response<IReadOnlyList<PortfolioDto>>> Handle(ListPortfoliosQuery request, CancellationToken cancellationToken) => service.List(request, cancellationToken);
}
