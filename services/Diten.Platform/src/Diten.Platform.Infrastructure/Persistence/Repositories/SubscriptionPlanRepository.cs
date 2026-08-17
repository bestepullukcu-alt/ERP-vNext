using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class SubscriptionPlanRepository : GlobalRepository<SubscriptionPlan>, ISubscriptionPlanRepository
{
    public SubscriptionPlanRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "platform_subscription_plans")
    {
    }

    public async Task<SubscriptionPlan?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var filter = Builders<SubscriptionPlan>.Filter.And(
            ExecutionFilter,
            Builders<SubscriptionPlan>.Filter.Eq(x => x.Code, code));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
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

        return await Collection.Find(Builders<SubscriptionPlan>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task<SubscriptionPlan?> GetActiveDefaultAsync(Guid? excludeId = null, CancellationToken ct = default)
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

        return await Collection.Find(Builders<SubscriptionPlan>.Filter.And(filters)).FirstOrDefaultAsync(ct);
    }

    public async Task UpdateAsync(SubscriptionPlan plan, CancellationToken ct = default)
    {
        plan.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<SubscriptionPlan>.Filter.And(
            ExecutionFilter,
            Builders<SubscriptionPlan>.Filter.Eq(x => x.Id, plan.Id));
        await Collection.ReplaceOneAsync(filter, plan, cancellationToken: ct);
    }

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

