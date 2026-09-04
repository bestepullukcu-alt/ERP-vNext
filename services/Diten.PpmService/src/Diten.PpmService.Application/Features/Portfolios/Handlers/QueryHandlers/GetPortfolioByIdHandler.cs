using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed class GetPortfolioByIdHandler(PortfolioService service) : IRequestHandler<GetPortfolioByIdQuery, Response<PortfolioDto>>
{
    public Task<Response<PortfolioDto>> Handle(GetPortfolioByIdQuery request, CancellationToken cancellationToken) => service.GetById(request, cancellationToken);
}
