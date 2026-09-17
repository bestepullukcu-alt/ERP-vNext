using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed class ChangePortfolioOwnerHandler(PortfolioService service) : IRequestHandler<ChangePortfolioOwnerCommand, Response<PortfolioOwnerReceipt>>
{
    public Task<Response<PortfolioOwnerReceipt>> Handle(ChangePortfolioOwnerCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.ChangeOwner(request, ct);
    }
}
