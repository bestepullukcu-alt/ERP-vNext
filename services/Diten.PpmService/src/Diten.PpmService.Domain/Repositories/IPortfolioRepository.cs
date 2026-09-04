using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Domain.Repositories;

public interface IPortfolioRepository : IRepository<Portfolio>
{
    Task AdvanceInvestmentCaseCollectionFenceAsync(Portfolio portfolio, CancellationToken cancellationToken);
}
