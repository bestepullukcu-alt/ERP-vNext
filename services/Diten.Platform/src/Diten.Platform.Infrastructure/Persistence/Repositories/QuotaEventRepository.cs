using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class QuotaEventRepository : TenantRepository<QuotaEvent>, IQuotaEventRepository
{
    private readonly IPlatformDbContext _dbContext;
    public QuotaEventRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.QuotaEvents)
    {
        _dbContext = dbContext;
    }

    public async Task<QuotaEvent> CreateAsync(IPlatformTransactionSession session, QuotaEvent quotaEvent, CancellationToken ct = default)
    {
        await Collection.InsertOneAsync(
            PlatformMongoTransactionSession.Require(session, _dbContext),
            quotaEvent,
            cancellationToken: ct);
        return quotaEvent;
    }

    public override async Task<QuotaEvent> CreateAsync(QuotaEvent quotaEvent, CancellationToken ct = default)
    {
        await Collection.InsertOneAsync(quotaEvent, cancellationToken: ct);
        return quotaEvent;
    }

    public async Task<bool> ExistsAsync(Guid tenantId, string quotaKey, string source, string? operationId, string? sourceReference, bool isRejected, CancellationToken ct = default)
        => await ExistsCoreAsync(null, tenantId, quotaKey, source, operationId, sourceReference, isRejected, ct);

    public Task<bool> ExistsAsync(IPlatformTransactionSession session, Guid tenantId, string quotaKey, string source, string? operationId, string? sourceReference, bool isRejected, CancellationToken ct = default) =>
        ExistsCoreAsync(PlatformMongoTransactionSession.Require(session, _dbContext), tenantId, quotaKey, source, operationId, sourceReference, isRejected, ct);

    private async Task<bool> ExistsCoreAsync(IClientSessionHandle? session, Guid tenantId, string quotaKey, string source, string? operationId, string? sourceReference, bool isRejected, CancellationToken ct)
    {
        var filters = new List<FilterDefinition<QuotaEvent>>
        {
            Builders<QuotaEvent>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<QuotaEvent>.Filter.Eq(x => x.QuotaKey, quotaKey),
            Builders<QuotaEvent>.Filter.Eq(x => x.Source, source),
            Builders<QuotaEvent>.Filter.Eq(x => x.IsRejected, isRejected),
            Builders<QuotaEvent>.Filter.Eq(x => x.IsDeleted, false)
        };

        if (!string.IsNullOrWhiteSpace(operationId))
        {
            filters.Add(Builders<QuotaEvent>.Filter.Eq(x => x.OperationId, operationId));
        }

        if (!string.IsNullOrWhiteSpace(sourceReference))
        {
            filters.Add(Builders<QuotaEvent>.Filter.Eq(x => x.SourceReference, sourceReference));
        }

        var filter = Builders<QuotaEvent>.Filter.And(filters);
        return session is null
            ? await Collection.Find(filter).AnyAsync(ct)
            : await Collection.Find(session, filter).AnyAsync(ct);
    }
}
