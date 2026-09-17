namespace Diten.PpmService.Application.Features.Portfolios;

// A trusted adapter must prove PMO authorization separately from target User eligibility.
// No production adapter is registered until CT provides the exact external contract.
public interface IPortfolioOwnerActionAuthority
{
    Task<PortfolioAuthorityEvidence> CanManageAsync(PortfolioAuthorityScope scope, CancellationToken ct);
    Task<PortfolioOwnerEvidence> EvaluateAsync(PortfolioAuthorityScope scope, CancellationToken ct);
    Task<PortfolioOwnerCandidatesEvidence> CandidatesAsync(PortfolioAuthorityScope scope, CancellationToken ct);
}
