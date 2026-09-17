namespace Diten.PpmService.Application.Features.Portfolios;

// SOP-0029 is owned externally. Missing evidence never implies creator-only or default access.
public interface IPortfolioRecordAccessAuthority
{
    Task<PortfolioAuthorityEvidence> EvaluateAsync(PortfolioAuthorityScope scope, CancellationToken ct);
}
