using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed class TransitionPortfolioLifecycleHandler(PortfolioService service) : IRequestHandler<TransitionPortfolioLifecycleCommand, Response<PortfolioDto>>
{
    public Task<Response<PortfolioDto>> Handle(TransitionPortfolioLifecycleCommand request, CancellationToken cancellationToken) => service.Transition(request, cancellationToken);
}
