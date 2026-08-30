using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed class UpdatePortfolioHandler(PortfolioService service) : IRequestHandler<UpdatePortfolioCommand, Response<PortfolioDto>>
{
    public Task<Response<PortfolioDto>> Handle(UpdatePortfolioCommand request, CancellationToken cancellationToken) => service.Update(request, cancellationToken);
}
