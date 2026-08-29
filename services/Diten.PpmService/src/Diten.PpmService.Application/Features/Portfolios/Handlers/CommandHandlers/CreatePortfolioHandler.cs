using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed class CreatePortfolioHandler(PortfolioService service) : IRequestHandler<CreatePortfolioCommand, Response<PortfolioDto>>
{
    public Task<Response<PortfolioDto>> Handle(CreatePortfolioCommand request, CancellationToken cancellationToken) => service.Create(request, cancellationToken);
}
