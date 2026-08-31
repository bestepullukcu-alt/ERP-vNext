using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Persistence.Mongo;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.Repositories;


public sealed class BenefitCommitmentRepository
    : MongoRepository<BenefitCommitment>, IBenefitCommitmentRepository
{
    private readonly PpmMongoContext _context;

    public BenefitCommitmentRepository(PpmMongoContext context)
        : base(context, context.BenefitCommitments) => _context = context;

    public async Task<bool> ExistsForInvestmentCaseAsync(
        Guid tenantId,
        Guid investmentCaseId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || investmentCaseId == Guid.Empty)
            throw new ArgumentException("TenantId and InvestmentCaseId are required.");

        var filter = Builders<BenefitCommitment>.Filter.Eq(x => x.TenantId, tenantId) &
                     Builders<BenefitCommitment>.Filter.Eq(x => x.InvestmentCaseId, investmentCaseId) &
                     Builders<BenefitCommitment>.Filter.Eq(x => x.IsDeleted, false);
        try
        {
            var session = _context.CurrentSession;
            return session is null
                ? await _context.BenefitCommitments.Find(filter).AnyAsync(cancellationToken)
                : await _context.BenefitCommitments.Find(session, filter).AnyAsync(cancellationToken);
        }
        catch (MongoException exception)
        {
            throw new TransactionUnavailableException("Mongo persistence is unavailable.", exception);
        }
    }
}
