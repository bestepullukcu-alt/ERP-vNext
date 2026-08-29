using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Persistence.Mongo;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.Repositories;

public abstract class MongoRepository<T>(
    PpmMongoContext context,
    IMongoCollection<T> collection) : IRepository<T>
    where T : EntityBase
{
    public async Task<T?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var filter = ActiveTenantFilter(tenantId) &
                     Builders<T>.Filter.Eq(entity => entity.Id, id);

        try
        {
            var session = context.CurrentSession;
            return session is null
                ? await collection.Find(filter).FirstOrDefaultAsync(cancellationToken)
                : await collection.Find(session, filter).FirstOrDefaultAsync(cancellationToken);
        }
        catch (MongoException exception) { throw Unavailable(exception); }
    }

    public async Task<IReadOnlyList<T>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var filter = ActiveTenantFilter(tenantId);
        var sort = Builders<T>.Sort.Ascending("Code");
        try
        {
            var session = context.CurrentSession;
            return session is null
                ? await collection.Find(filter).Sort(sort).ToListAsync(cancellationToken)
                : await collection.Find(session, filter).Sort(sort).ToListAsync(cancellationToken);
        }
        catch (MongoException exception) { throw Unavailable(exception); }
    }

    public async Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var filter = ActiveTenantFilter(tenantId) &
                     Builders<T>.Filter.Eq("Code", normalizedCode);

        if (excludingId.HasValue)
            filter &= Builders<T>.Filter.Ne(entity => entity.Id, excludingId.Value);

        try
        {
            var session = context.CurrentSession;
            return session is null
                ? await collection.Find(filter).AnyAsync(cancellationToken)
                : await collection.Find(session, filter).AnyAsync(cancellationToken);
        }
        catch (MongoException exception) { throw Unavailable(exception); }
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken)
    {
        ValidateEntityScope(entity);
        var session = context.RequireTransaction();
        await collection.InsertOneAsync(session, entity, cancellationToken: cancellationToken);
    }

    public async Task ReplaceAsync(
        T entity,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        ValidateEntityScope(entity);
        if (expectedVersion < 1 || entity.Version != expectedVersion + 1)
            throw new OptimisticConcurrencyException(
                $"{typeof(T).Name} expected version does not match the mutation base version.");

        var filter = ActiveTenantFilter(entity.TenantId) &
                     Builders<T>.Filter.Eq(item => item.Id, entity.Id) &
                     Builders<T>.Filter.Eq(item => item.Version, expectedVersion);

        var session = context.RequireTransaction();
        var result = await collection.ReplaceOneAsync(
            session,
            filter,
            entity,
            new ReplaceOptions { IsUpsert = false },
            cancellationToken);

        if (result.MatchedCount != 1)
            throw new OptimisticConcurrencyException(
                $"{typeof(T).Name} was not found in the tenant or its version changed.");
    }

    private static FilterDefinition<T> ActiveTenantFilter(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        return Builders<T>.Filter.Eq(entity => entity.TenantId, tenantId) &
               Builders<T>.Filter.Eq(entity => entity.IsDeleted, false);
    }

    private static void ValidateEntityScope(T entity)
    {
        if (entity.Id == Guid.Empty || entity.TenantId == Guid.Empty)
            throw new InvalidOperationException("PPM entities require non-empty Id and TenantId.");
        if (entity.CreatedAtUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("CreatedAtUtc must be UTC.");
        if (entity.UpdatedAtUtc is { Kind: not DateTimeKind.Utc })
            throw new InvalidOperationException("UpdatedAtUtc must be UTC.");
        if (entity.DeletedAtUtc is { Kind: not DateTimeKind.Utc })
            throw new InvalidOperationException("DeletedAtUtc must be UTC.");
    }

    private static TransactionUnavailableException Unavailable(MongoException exception) =>
        new("Mongo persistence is unavailable.", exception);
}

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

public sealed class InitiativeRepository(PpmMongoContext context)
    : MongoRepository<Initiative>(context, context.Initiatives), IInitiativeRepository;

public sealed class ProgramRepository(PpmMongoContext context)
    : MongoRepository<Program>(context, context.Programs), IProgramRepository;

public sealed class ProjectRepository(PpmMongoContext context)
    : MongoRepository<Project>(context, context.Projects), IProjectRepository;

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
