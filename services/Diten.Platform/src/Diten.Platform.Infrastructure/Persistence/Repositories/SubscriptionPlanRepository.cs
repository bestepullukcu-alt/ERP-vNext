using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class SubscriptionPlanRepository : GlobalRepository<SubscriptionPlan>, ITransactionalSubscriptionPlanRepository
{
    private readonly IPlatformDbContext _dbContext;

    public SubscriptionPlanRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "platform_subscription_plans")
    {
        _dbContext = dbContext;
    }

    public async Task<SubscriptionPlan> CreateAsync(IPlatformTransactionSession session, SubscriptionPlan plan, CancellationToken ct = default)
    {
        await Collection.InsertOneAsync(PlatformMongoTransactionSession.Require(session, _dbContext), plan, cancellationToken: ct);
        return plan;
    }

    public async Task<SubscriptionPlan?> GetByIdAsync(IPlatformTransactionSession session, Guid id, CancellationToken ct = default) =>
        await Collection.Find(PlatformMongoTransactionSession.Require(session, _dbContext), Builders<SubscriptionPlan>.Filter.And(
            ExecutionFilter, Builders<SubscriptionPlan>.Filter.Eq(x => x.Id, id))).FirstOrDefaultAsync(ct);

    public Task<SubscriptionPlan?> GetByCodeAsync(IPlatformTransactionSession session, string code, CancellationToken ct = default) =>
        GetByCodeCoreAsync(PlatformMongoTransactionSession.Require(session, _dbContext), code, ct);

    public async Task<SubscriptionPlan?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await GetByCodeCoreAsync(null, code, ct);

    private async Task<SubscriptionPlan?> GetByCodeCoreAsync(IClientSessionHandle? session, string code, CancellationToken ct)
    {
        var filter = Builders<SubscriptionPlan>.Filter.And(
            ExecutionFilter,
            Builders<SubscriptionPlan>.Filter.Eq(x => x.Code, code));
        return session is null
            ? await Collection.Find(filter).FirstOrDefaultAsync(ct)
            : await Collection.Find(session, filter).FirstOrDefaultAsync(ct);
    }

    public Task<bool> ExistsByCodeAsync(IPlatformTransactionSession session, string code, Guid? excludeId = null, CancellationToken ct = default) =>
        ExistsByCodeCoreAsync(PlatformMongoTransactionSession.Require(session, _dbContext), code, excludeId, ct);

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
        => await ExistsByCodeCoreAsync(null, code, excludeId, ct);

    private async Task<bool> ExistsByCodeCoreAsync(IClientSessionHandle? session, string code, Guid? excludeId, CancellationToken ct)
    {
        var filters = new List<FilterDefinition<SubscriptionPlan>>
        {
            ExecutionFilter,
            Builders<SubscriptionPlan>.Filter.Eq(x => x.Code, code)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<SubscriptionPlan>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        var filter = Builders<SubscriptionPlan>.Filter.And(filters);
        return session is null
            ? await Collection.Find(filter).AnyAsync(ct)
            : await Collection.Find(session, filter).AnyAsync(ct);
    }

    public Task<SubscriptionPlan?> GetActiveDefaultAsync(IPlatformTransactionSession session, Guid? excludeId = null, CancellationToken ct = default) =>
        GetActiveDefaultCoreAsync(PlatformMongoTransactionSession.Require(session, _dbContext), excludeId, ct);

    public async Task<SubscriptionPlan?> GetActiveDefaultAsync(Guid? excludeId = null, CancellationToken ct = default)
        => await GetActiveDefaultCoreAsync(null, excludeId, ct);

    private async Task<SubscriptionPlan?> GetActiveDefaultCoreAsync(IClientSessionHandle? session, Guid? excludeId, CancellationToken ct)
    {
        var filters = new List<FilterDefinition<SubscriptionPlan>>
        {
            ExecutionFilter,
            Builders<SubscriptionPlan>.Filter.Eq(x => x.IsActive, true),
            Builders<SubscriptionPlan>.Filter.Eq(x => x.IsDefault, true)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<SubscriptionPlan>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        var filter = Builders<SubscriptionPlan>.Filter.And(filters);
        return session is null
            ? await Collection.Find(filter).FirstOrDefaultAsync(ct)
            : await Collection.Find(session, filter).FirstOrDefaultAsync(ct);
    }

    public Task UpdateAsync(IPlatformTransactionSession session, SubscriptionPlan plan, CancellationToken ct = default) =>
        UpdateCoreAsync(PlatformMongoTransactionSession.Require(session, _dbContext), plan, ct);

    [Obsolete("Authoritative subscription-plan mutations require an explicit Platform transaction session.")]
    public Task UpdateAsync(SubscriptionPlan plan, CancellationToken ct = default) =>
        throw new PlatformTransactionUnavailableException(
            "Sessionless subscription-plan mutation is disabled until the caller supplies the Platform transaction session.");

    private async Task UpdateCoreAsync(IClientSessionHandle? session, SubscriptionPlan plan, CancellationToken ct)
    {
        plan.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<SubscriptionPlan>.Filter.And(
            ExecutionFilter,
            Builders<SubscriptionPlan>.Filter.Eq(x => x.Id, plan.Id));
        if (session is null)
        {
            await Collection.ReplaceOneAsync(filter, plan, cancellationToken: ct);
        }
        else
        {
            await Collection.ReplaceOneAsync(session, filter, plan, cancellationToken: ct);
        }
    }

    [Obsolete("Authoritative subscription-plan mutations require an explicit Platform transaction session.")]
    public override Task<SubscriptionPlan> CreateAsync(SubscriptionPlan plan, CancellationToken ct = default) =>
        throw new PlatformTransactionUnavailableException(
            "Sessionless subscription-plan mutation is disabled until the caller supplies the Platform transaction session.");


    public async Task<(IReadOnlyList<SubscriptionPlan> Items, long TotalCount)> QueryAsync(SubscriptionPlansQuery query, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<SubscriptionPlan>> { ExecutionFilter };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var escaped = System.Text.RegularExpressions.Regex.Escape(query.Search.Trim());
            var regex = new BsonRegularExpression(escaped, "i");
            filters.Add(Builders<SubscriptionPlan>.Filter.Or(
                Builders<SubscriptionPlan>.Filter.Regex(x => x.Code, regex),
                Builders<SubscriptionPlan>.Filter.Regex(x => x.Name, regex),
                Builders<SubscriptionPlan>.Filter.Regex(x => x.Description, regex)));
        }

        if (query.IsActive.HasValue)
        {
            filters.Add(Builders<SubscriptionPlan>.Filter.Eq(x => x.IsActive, query.IsActive.Value));
        }

        if (query.IsTrialPlan.HasValue)
        {
            filters.Add(Builders<SubscriptionPlan>.Filter.Eq(x => x.IsTrialPlan, query.IsTrialPlan.Value));
        }

        var filter = Builders<SubscriptionPlan>.Filter.And(filters);
        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await Collection.Find(filter)
            .Sort(BuildSort(query.Sort))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> GetActiveAsync(CancellationToken ct = default)
    {
        var filter = Builders<SubscriptionPlan>.Filter.And(
            ExecutionFilter,
            Builders<SubscriptionPlan>.Filter.Eq(x => x.IsActive, true));

        return await Collection.Find(filter)
            .Sort(Builders<SubscriptionPlan>.Sort.Ascending(x => x.SortOrder).Ascending(x => x.Code))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> GetByIncludedModuleKeyAsync(string moduleKey, CancellationToken ct = default)
    {
        var normalized = (moduleKey ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length == 0)
        {
            return [];
        }

        var filter = Builders<SubscriptionPlan>.Filter.And(
            ExecutionFilter,
            Builders<SubscriptionPlan>.Filter.AnyEq(x => x.IncludedModuleKeys, normalized));

        return await Collection.Find(filter)
            .Sort(Builders<SubscriptionPlan>.Sort.Ascending(x => x.SortOrder).Ascending(x => x.Code))
            .ToListAsync(ct);
    }

    public async Task<SubscriptionPlanSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var total = await Collection.CountDocumentsAsync(ExecutionFilter, cancellationToken: ct);
        var active = await Collection.CountDocumentsAsync(
            Builders<SubscriptionPlan>.Filter.And(ExecutionFilter, Builders<SubscriptionPlan>.Filter.Eq(x => x.IsActive, true)),
            cancellationToken: ct);
        var trialPlans = await Collection.CountDocumentsAsync(
            Builders<SubscriptionPlan>.Filter.And(ExecutionFilter, Builders<SubscriptionPlan>.Filter.Eq(x => x.IsTrialPlan, true)),
            cancellationToken: ct);
        var paidPlans = await Collection.CountDocumentsAsync(
            Builders<SubscriptionPlan>.Filter.And(ExecutionFilter, Builders<SubscriptionPlan>.Filter.Eq(x => x.IsTrialPlan, false)),
            cancellationToken: ct);

        return new SubscriptionPlanSummary(total, active, trialPlans, paidPlans);
    }

    private static SortDefinition<SubscriptionPlan> BuildSort(string? sort)
    {
        var normalized = string.IsNullOrWhiteSpace(sort) ? "sortOrder" : sort.Trim();
        var descending = normalized.StartsWith("-", StringComparison.Ordinal);
        var field = descending ? normalized[1..] : normalized;

        return field.ToLowerInvariant() switch
        {
            "code" => descending ? Builders<SubscriptionPlan>.Sort.Descending(x => x.Code) : Builders<SubscriptionPlan>.Sort.Ascending(x => x.Code),
            "name" => descending ? Builders<SubscriptionPlan>.Sort.Descending(x => x.Name) : Builders<SubscriptionPlan>.Sort.Ascending(x => x.Name),
            "isactive" => descending ? Builders<SubscriptionPlan>.Sort.Descending(x => x.IsActive) : Builders<SubscriptionPlan>.Sort.Ascending(x => x.IsActive),
            "isdefault" => descending ? Builders<SubscriptionPlan>.Sort.Descending(x => x.IsDefault) : Builders<SubscriptionPlan>.Sort.Ascending(x => x.IsDefault),
            "createdat" => descending ? Builders<SubscriptionPlan>.Sort.Descending(x => x.CreatedAt) : Builders<SubscriptionPlan>.Sort.Ascending(x => x.CreatedAt),
            "updatedat" => descending ? Builders<SubscriptionPlan>.Sort.Descending(x => x.UpdatedAt) : Builders<SubscriptionPlan>.Sort.Ascending(x => x.UpdatedAt),
            _ => descending ? Builders<SubscriptionPlan>.Sort.Descending(x => x.SortOrder) : Builders<SubscriptionPlan>.Sort.Ascending(x => x.SortOrder)
        };
    }
}
