using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed class SoftDeletePortfolioHandler(PortfolioService service) : IRequestHandler<SoftDeletePortfolioCommand, Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(SoftDeletePortfolioCommand request, CancellationToken cancellationToken) => service.SoftDelete(request, cancellationToken);
}
