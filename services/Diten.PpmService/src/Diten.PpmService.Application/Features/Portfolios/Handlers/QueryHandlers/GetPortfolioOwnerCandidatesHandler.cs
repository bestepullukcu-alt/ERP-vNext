using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed class GetPortfolioOwnerCandidatesHandler(PortfolioService service) : IRequestHandler<GetPortfolioOwnerCandidatesQuery, Response<IReadOnlyList<PortfolioOwnerCandidate>>>
{
    public Task<Response<IReadOnlyList<PortfolioOwnerCandidate>>> Handle(GetPortfolioOwnerCandidatesQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.OwnerCandidates(request, ct);
    }
}
