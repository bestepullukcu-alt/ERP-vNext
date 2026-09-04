using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Persistence.Mongo;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.Repositories;


public sealed class InvestmentCaseRepository : MongoRepository<InvestmentCase>, IInvestmentCaseRepository
{
    private readonly PpmMongoContext _context;
    public InvestmentCaseRepository(PpmMongoContext context) : base(context, context.InvestmentCases) => _context = context;
    public async Task AdvanceBenefitCommitmentCollectionFenceAsync(InvestmentCase investmentCase, CancellationToken cancellationToken)
    {
        var expected = investmentCase.BenefitCommitmentCollectionFence;
        var filter = Builders<InvestmentCase>.Filter.Eq(x => x.TenantId, investmentCase.TenantId) &
                     Builders<InvestmentCase>.Filter.Eq(x => x.Id, investmentCase.Id) &
                     Builders<InvestmentCase>.Filter.Eq(x => x.IsDeleted, false) &
                     Builders<InvestmentCase>.Filter.Eq(x => x.BenefitCommitmentCollectionFence, expected);
        var result = await _context.InvestmentCases.UpdateOneAsync(_context.RequireTransaction(), filter,
            Builders<InvestmentCase>.Update.Inc(x => x.BenefitCommitmentCollectionFence, 1), cancellationToken: cancellationToken);
        if (result.MatchedCount != 1) throw new OptimisticConcurrencyException("Investment case collection fence changed.");
        investmentCase.AdvanceBenefitCommitmentCollectionFence();
    }

    public async Task<bool> ExistsForPortfolioAsync(Guid tenantId, Guid portfolioId, CancellationToken cancellationToken)
    {
        var filter = Builders<InvestmentCase>.Filter.Eq(x => x.TenantId, tenantId) &
                     Builders<InvestmentCase>.Filter.Eq(x => x.PortfolioId, portfolioId) &
                     Builders<InvestmentCase>.Filter.Eq(x => x.IsDeleted, false);
        var session = _context.CurrentSession;
        return session is null
            ? await _context.InvestmentCases.Find(filter).AnyAsync(cancellationToken)
            : await _context.InvestmentCases.Find(session, filter).AnyAsync(cancellationToken);
    }
}
