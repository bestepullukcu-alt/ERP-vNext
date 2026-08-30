using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Persistence.Mongo;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.Repositories;


public sealed class PortfolioRepository : MongoRepository<Portfolio>, IPortfolioRepository
{
    private readonly PpmMongoContext _context;
    public PortfolioRepository(PpmMongoContext context) : base(context, context.Portfolios) => _context = context;
    public async Task AdvanceInvestmentCaseCollectionFenceAsync(Portfolio portfolio, CancellationToken cancellationToken)
    {
        var expected = portfolio.InvestmentCaseCollectionFence;
        var filter = Builders<Portfolio>.Filter.Eq(x => x.TenantId, portfolio.TenantId) &
                     Builders<Portfolio>.Filter.Eq(x => x.Id, portfolio.Id) &
                     Builders<Portfolio>.Filter.Eq(x => x.IsDeleted, false) &
                     Builders<Portfolio>.Filter.Eq(x => x.InvestmentCaseCollectionFence, expected);
        var result = await _context.Portfolios.UpdateOneAsync(_context.RequireTransaction(), filter,
            Builders<Portfolio>.Update.Inc(x => x.InvestmentCaseCollectionFence, 1), cancellationToken: cancellationToken);
        if (result.MatchedCount != 1) throw new OptimisticConcurrencyException("Portfolio collection fence changed.");
        portfolio.AdvanceInvestmentCaseCollectionFence();
    }
}
