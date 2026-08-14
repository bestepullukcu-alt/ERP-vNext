using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class QuotaUsageRepository : TenantRepository<QuotaUsage>, IQuotaUsageRepository
{
    private readonly IPlatformDbContext _dbContext;

    public QuotaUsageRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "quota_usages")
    {
        _dbContext = dbContext;
    }

    public Task<QuotaMutationResult> TryConsumeAtomicAsync(IPlatformTransactionSession session, Guid tenantId, string quotaKey, decimal amount, DateTimeOffset now, CancellationToken ct = default) =>
        TryConsumeAtomicCoreAsync(PlatformMongoTransactionSession.Require(session, _dbContext), tenantId, quotaKey, amount, now, ct);

    public Task<QuotaMutationResult> TryReleaseAtomicAsync(IPlatformTransactionSession session, Guid tenantId, string quotaKey, decimal amount, DateTimeOffset now, CancellationToken ct = default) =>
        TryReleaseAtomicCoreAsync(PlatformMongoTransactionSession.Require(session, _dbContext), tenantId, quotaKey, amount, now, ct);

    public Task<QuotaUsage?> SetCurrentValueAsync(IPlatformTransactionSession session, Guid tenantId, string quotaKey, decimal currentValue, DateTimeOffset now, CancellationToken ct = default) =>
        SetCurrentValueCoreAsync(PlatformMongoTransactionSession.Require(session, _dbContext), tenantId, quotaKey, currentValue, now, ct);

    public override async Task<QuotaUsage> CreateAsync(QuotaUsage usage, CancellationToken ct = default)
    {
        await Collection.InsertOneAsync(usage, cancellationToken: ct);
        return usage;
    }

    public async Task<QuotaUsage?> GetByTenantAndKeyAsync(Guid tenantId, string quotaKey, CancellationToken ct = default)
        => await GetByTenantAndKeyCoreAsync(null, tenantId, quotaKey, ct);

    public Task<QuotaUsage?> GetByTenantAndKeyAsync(IPlatformTransactionSession session, Guid tenantId, string quotaKey, CancellationToken ct = default) =>
        GetByTenantAndKeyCoreAsync(PlatformMongoTransactionSession.Require(session, _dbContext), tenantId, quotaKey, ct);

    private async Task<QuotaUsage?> GetByTenantAndKeyCoreAsync(IClientSessionHandle? session, Guid tenantId, string quotaKey, CancellationToken ct)
    {
        var filter = Builders<QuotaUsage>.Filter.And(
            Builders<QuotaUsage>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<QuotaUsage>.Filter.Eq(x => x.QuotaKey, quotaKey),
            Builders<QuotaUsage>.Filter.Eq(x => x.IsDeleted, false));

        return session is null
            ? await Collection.Find(filter).FirstOrDefaultAsync(ct)
            : await Collection.Find(session, filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<QuotaUsage>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var filter = Builders<QuotaUsage>.Filter.And(
            Builders<QuotaUsage>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<QuotaUsage>.Filter.Eq(x => x.IsDeleted, false));

        return await Collection.Find(filter)
            .Sort(Builders<QuotaUsage>.Sort.Ascending(x => x.QuotaKey))
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(Guid tenantId, string quotaKey, CancellationToken ct = default)
    {
        var filter = Builders<QuotaUsage>.Filter.And(
            Builders<QuotaUsage>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<QuotaUsage>.Filter.Eq(x => x.QuotaKey, quotaKey),
            Builders<QuotaUsage>.Filter.Eq(x => x.IsDeleted, false));

        return await Collection.Find(filter).AnyAsync(ct);
    }

    public async Task<QuotaMutationResult> TryConsumeAtomicAsync(Guid tenantId, string quotaKey, decimal amount, DateTimeOffset now, CancellationToken ct = default)
        => await TryConsumeAtomicCoreAsync(null, tenantId, quotaKey, amount, now, ct);

    private async Task<QuotaMutationResult> TryConsumeAtomicCoreAsync(IClientSessionHandle? session, Guid tenantId, string quotaKey, decimal amount, DateTimeOffset now, CancellationToken ct)
    {
        var quotaLimitFilter = new BsonDocumentFilterDefinition<QuotaUsage>(new BsonDocument("$expr", new BsonDocument("$lte", new BsonArray
        {
            new BsonDocument("$add", new BsonArray { "$" + nameof(QuotaUsage.CurrentValue), new BsonDecimal128(amount) }),
            "$" + nameof(QuotaUsage.LimitValue)
        })));

        var filter = Builders<QuotaUsage>.Filter.And(
            Builders<QuotaUsage>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<QuotaUsage>.Filter.Eq(x => x.QuotaKey, quotaKey),
            Builders<QuotaUsage>.Filter.Eq(x => x.IsDeleted, false),
            quotaLimitFilter);

        var update = Builders<QuotaUsage>.Update
            .Inc(x => x.CurrentValue, amount)
            .Set(x => x.LastUpdatedUtc, now)
            .Set(x => x.UpdatedAt, now);

        var options = new FindOneAndUpdateOptions<QuotaUsage>
        {
            ReturnDocument = ReturnDocument.After
        };

        var updated = session is null
            ? await Collection.FindOneAndUpdateAsync(filter, update, options, ct)
            : await Collection.FindOneAndUpdateAsync(session, filter, update, options, ct);
        return new QuotaMutationResult(updated is not null, updated);
    }

    public async Task<QuotaMutationResult> TryReleaseAtomicAsync(Guid tenantId, string quotaKey, decimal amount, DateTimeOffset now, CancellationToken ct = default)
        => await TryReleaseAtomicCoreAsync(null, tenantId, quotaKey, amount, now, ct);

    private async Task<QuotaMutationResult> TryReleaseAtomicCoreAsync(IClientSessionHandle? session, Guid tenantId, string quotaKey, decimal amount, DateTimeOffset now, CancellationToken ct)
    {
        var filter = Builders<QuotaUsage>.Filter.And(
            Builders<QuotaUsage>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<QuotaUsage>.Filter.Eq(x => x.QuotaKey, quotaKey),
            Builders<QuotaUsage>.Filter.Eq(x => x.IsDeleted, false),
            Builders<QuotaUsage>.Filter.Gte(x => x.CurrentValue, amount));

        var update = Builders<QuotaUsage>.Update
            .Inc(x => x.CurrentValue, -amount)
            .Set(x => x.LastUpdatedUtc, now)
            .Set(x => x.UpdatedAt, now);

        var options = new FindOneAndUpdateOptions<QuotaUsage>
        {
            ReturnDocument = ReturnDocument.After
        };

        var updated = session is null
            ? await Collection.FindOneAndUpdateAsync(filter, update, options, ct)
            : await Collection.FindOneAndUpdateAsync(session, filter, update, options, ct);
        return new QuotaMutationResult(updated is not null, updated);
    }

    public async Task<QuotaUsage?> ResetPeriodAsync(Guid tenantId, string quotaKey, DateTimeOffset periodStart, DateTimeOffset periodEnd, DateTimeOffset now, CancellationToken ct = default)
    {
        var filter = Builders<QuotaUsage>.Filter.And(
            Builders<QuotaUsage>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<QuotaUsage>.Filter.Eq(x => x.QuotaKey, quotaKey),
            Builders<QuotaUsage>.Filter.Eq(x => x.IsDeleted, false));

        var update = Builders<QuotaUsage>.Update
            .Set(x => x.CurrentValue, 0)
            .Set(x => x.PeriodStart, periodStart)
            .Set(x => x.PeriodEnd, periodEnd)
            .Set(x => x.LastUpdatedUtc, now)
            .Set(x => x.WarningNotificationSentForPeriod, false)
            .Set(x => x.LimitBreachNotificationSentForPeriod, false)
            .Set(x => x.LastWarningNotifiedAtUtc, null)
            .Set(x => x.LastLimitBreachNotifiedAtUtc, null)
            .Set(x => x.UpdatedAt, now);

        return await Collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<QuotaUsage> { ReturnDocument = ReturnDocument.After },
            ct);
    }

    public async Task<QuotaUsage?> UpdateLimitAsync(Guid tenantId, string quotaKey, decimal limitValue, Guid subscriptionId, Guid planId, string source, string? overrideSource, DateTimeOffset now, CancellationToken ct = default)
    {
        var filter = Builders<QuotaUsage>.Filter.And(
            Builders<QuotaUsage>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<QuotaUsage>.Filter.Eq(x => x.QuotaKey, quotaKey),
            Builders<QuotaUsage>.Filter.Eq(x => x.IsDeleted, false));

        var update = Builders<QuotaUsage>.Update
            .Set(x => x.LimitValue, limitValue)
            .Set(x => x.SubscriptionId, subscriptionId)
            .Set(x => x.PlanId, planId)
            .Set(x => x.Source, source)
            .Set(x => x.OverrideSource, overrideSource)
            .Set(x => x.LastUpdatedUtc, now)
            .Set(x => x.UpdatedAt, now);

        return await Collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<QuotaUsage> { ReturnDocument = ReturnDocument.After },
            ct);
    }

    public async Task<QuotaUsage?> SetCurrentValueAsync(Guid tenantId, string quotaKey, decimal currentValue, DateTimeOffset now, CancellationToken ct = default)
        => await SetCurrentValueCoreAsync(null, tenantId, quotaKey, currentValue, now, ct);

    private async Task<QuotaUsage?> SetCurrentValueCoreAsync(IClientSessionHandle? session, Guid tenantId, string quotaKey, decimal currentValue, DateTimeOffset now, CancellationToken ct)
    {
        var filter = Builders<QuotaUsage>.Filter.And(
            Builders<QuotaUsage>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<QuotaUsage>.Filter.Eq(x => x.QuotaKey, quotaKey),
            Builders<QuotaUsage>.Filter.Eq(x => x.IsDeleted, false));

        var update = Builders<QuotaUsage>.Update
            .Set(x => x.CurrentValue, currentValue)
            .Set(x => x.LastUpdatedUtc, now)
            .Set(x => x.UpdatedAt, now);

        var options = new FindOneAndUpdateOptions<QuotaUsage> { ReturnDocument = ReturnDocument.After };
        return session is null
            ? await Collection.FindOneAndUpdateAsync(filter, update, options, ct)
            : await Collection.FindOneAndUpdateAsync(session, filter, update, options, ct);
    }

    public async Task<QuotaUsage?> MarkNotificationStateAsync(Guid tenantId, string quotaKey, bool warningSent, bool breachSent, DateTimeOffset now, CancellationToken ct = default)
    {
        var filter = Builders<QuotaUsage>.Filter.And(
            Builders<QuotaUsage>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<QuotaUsage>.Filter.Eq(x => x.QuotaKey, quotaKey),
            Builders<QuotaUsage>.Filter.Eq(x => x.IsDeleted, false));

        var updates = new List<UpdateDefinition<QuotaUsage>>
        {
            Builders<QuotaUsage>.Update.Set(x => x.UpdatedAt, now)
        };

        if (warningSent)
        {
            updates.Add(Builders<QuotaUsage>.Update.Set(x => x.WarningNotificationSentForPeriod, true));
            updates.Add(Builders<QuotaUsage>.Update.Set(x => x.LastWarningNotifiedAtUtc, now));
        }

        if (breachSent)
        {
            updates.Add(Builders<QuotaUsage>.Update.Set(x => x.LimitBreachNotificationSentForPeriod, true));
            updates.Add(Builders<QuotaUsage>.Update.Set(x => x.LastLimitBreachNotifiedAtUtc, now));
        }

        return await Collection.FindOneAndUpdateAsync(
            filter,
            Builders<QuotaUsage>.Update.Combine(updates),
            new FindOneAndUpdateOptions<QuotaUsage> { ReturnDocument = ReturnDocument.After },
            ct);
    }
}
