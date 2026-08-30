using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Domain.Repositories;

public interface IInvestmentCaseRepository : IRepository<InvestmentCase>
{
    Task AdvanceBenefitCommitmentCollectionFenceAsync(InvestmentCase investmentCase, CancellationToken cancellationToken);
    Task<bool> ExistsForPortfolioAsync(Guid tenantId, Guid portfolioId, CancellationToken cancellationToken);
}
