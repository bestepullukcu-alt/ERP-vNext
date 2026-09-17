using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed class GetPortfolioPageAccessHandler(PortfolioService service) : IRequestHandler<GetPortfolioPageAccessQuery, Response<PortfolioPageAccess>>
{
    public Task<Response<PortfolioPageAccess>> Handle(GetPortfolioPageAccessQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.PageAccess(request, ct);
    }
}
