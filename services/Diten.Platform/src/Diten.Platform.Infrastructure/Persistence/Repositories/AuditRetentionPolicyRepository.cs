using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class AuditRetentionPolicyRepository : IAuditRetentionPolicyRepository
{
    private readonly IMongoCollection<AuditEventRetentionPolicy> _collection;

    public AuditRetentionPolicyRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<AuditEventRetentionPolicy>(AuditCollectionNames.AuditEventRetentionPolicies);
    }

    public async Task<AuditEventRetentionPolicy?> GetActivePolicyByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Audit retention policy id is required.", nameof(id));
        }

        var filter = Builders<AuditEventRetentionPolicy>.Filter.And(
            Builders<AuditEventRetentionPolicy>.Filter.Eq(x => x.Id, id),
            Builders<AuditEventRetentionPolicy>.Filter.Eq(x => x.IsActive, true),
            Builders<AuditEventRetentionPolicy>.Filter.Eq(x => x.IsDeleted, false));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<AuditEventRetentionPolicy?> GetActivePolicyAsync(AuditCategory category, string planTierCode, CancellationToken ct = default)
    {
        if (category == AuditCategory.Unknown)
        {
            throw new ArgumentException("Audit category is required.", nameof(category));
        }

        if (string.IsNullOrWhiteSpace(planTierCode))
        {
            throw new ArgumentException("Plan tier code is required.", nameof(planTierCode));
        }

        var normalizedPlanTierCode = planTierCode.Trim();
        var filter = Builders<AuditEventRetentionPolicy>.Filter.And(
            Builders<AuditEventRetentionPolicy>.Filter.Eq(x => x.Category, category),
            Builders<AuditEventRetentionPolicy>.Filter.Eq(x => x.PlanTierCode, normalizedPlanTierCode),
            Builders<AuditEventRetentionPolicy>.Filter.Eq(x => x.IsActive, true),
            Builders<AuditEventRetentionPolicy>.Filter.Eq(x => x.IsDeleted, false));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public Task<AuditEventRetentionPolicy?> GetDefaultPolicyAsync(AuditCategory category, CancellationToken ct = default)
    {
        return GetActivePolicyAsync(category, AuditEventRetentionPolicy.DefaultPlanTierCode, ct);
    }

    public async Task<IReadOnlyList<AuditEventRetentionPolicy>> GetActivePoliciesAsync(CancellationToken ct = default)
    {
        var filter = Builders<AuditEventRetentionPolicy>.Filter.And(
            Builders<AuditEventRetentionPolicy>.Filter.Eq(x => x.IsActive, true),
            Builders<AuditEventRetentionPolicy>.Filter.Eq(x => x.IsDeleted, false));

        return await _collection.Find(filter)
            .Sort(Builders<AuditEventRetentionPolicy>.Sort.Ascending(x => x.Category).Ascending(x => x.PlanTierCode))
            .ToListAsync(ct);
    }

    public async Task<bool> UpdateAsync(AuditEventRetentionPolicy policy, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.PlanTierCode = policy.PlanTierCode.Trim();
        policy.Validate();
        if (policy.IsDeleted)
        {
            throw new InvalidOperationException("Audit retention policy update cannot target a soft-deleted record.");
        }

        var filter = Builders<AuditEventRetentionPolicy>.Filter.And(
            Builders<AuditEventRetentionPolicy>.Filter.Eq(x => x.Id, policy.Id),
            Builders<AuditEventRetentionPolicy>.Filter.Eq(x => x.Category, policy.Category),
            Builders<AuditEventRetentionPolicy>.Filter.Eq(x => x.PlanTierCode, policy.PlanTierCode),
            Builders<AuditEventRetentionPolicy>.Filter.Eq(x => x.IsDeleted, false));

        var result = await _collection.ReplaceOneAsync(filter, policy, new ReplaceOptions { IsUpsert = false }, ct);
        return result.MatchedCount > 0;
    }
}
