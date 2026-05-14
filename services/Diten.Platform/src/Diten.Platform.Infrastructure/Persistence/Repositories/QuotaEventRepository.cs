using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class QuotaEventRepository : TenantRepository<QuotaEvent>, IQuotaEventRepository
{
    public QuotaEventRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "quota_events")
    {
    }

    public override async Task<QuotaEvent> CreateAsync(QuotaEvent quotaEvent, CancellationToken ct = default)
    {
        await Collection.InsertOneAsync(quotaEvent, cancellationToken: ct);
        return quotaEvent;
    }

    public async Task<bool> ExistsAsync(Guid tenantId, string quotaKey, string source, string? operationId, string? sourceReference, bool isRejected, CancellationToken ct = default)
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

        return await Collection.Find(Builders<QuotaEvent>.Filter.And(filters)).AnyAsync(ct);
    }
}
