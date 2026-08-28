using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0165 FU03 frequency policy persistence. Soft-delete aware and tenant scoped. No delete method exists:
/// closing a policy is a status update (inactive/archived), so history stays readable. EffectiveFrom / EffectiveTo
/// (DateTimeOffset → BSON array) are never sorted server-side; ordering is done in memory.
/// </summary>
public sealed class VisitFrequencyPolicyRepository : IVisitFrequencyPolicyRepository
{
    public const string CollectionName = "visit_frequency_policies";

    private readonly IMongoCollection<VisitFrequencyPolicy> _collection;

    public VisitFrequencyPolicyRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<VisitFrequencyPolicy>(CollectionName);
    }

    private static FilterDefinition<VisitFrequencyPolicy> Tenant(Guid tenantId)
        => Builders<VisitFrequencyPolicy>.Filter.Where(p => p.TenantId == tenantId && !p.IsDeleted);

    public async Task<VisitFrequencyPolicy?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<VisitFrequencyPolicy>.Filter.Eq(p => p.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<VisitFrequencyPolicy>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        // CreatedAt is a DateTimeOffset (BSON array) so it is not used as a server-side sort key; order in memory.
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderByDescending(p => p.CreatedAt).ToList();
    }

    public async Task<VisitFrequencyPolicy?> GetActiveByCodeAsync(Guid tenantId, string policyCode, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<VisitFrequencyPolicy>.Filter.Eq(p => p.PolicyCode, policyCode)
                & Builders<VisitFrequencyPolicy>.Filter.Ne(p => p.Status, FrequencyPolicyStatus.Archived))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<VisitFrequencyPolicy>> ListActiveByTargetsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> targetIds, CancellationToken cancellationToken)
    {
        if (targetIds is null || targetIds.Count == 0)
        {
            return [];
        }

        return await _collection
            .Find(Tenant(tenantId)
                & Builders<VisitFrequencyPolicy>.Filter.Eq(p => p.Status, FrequencyPolicyStatus.Active)
                & Builders<VisitFrequencyPolicy>.Filter.In(p => p.TargetId, targetIds.Distinct()))
            .ToListAsync(cancellationToken);
    }

    public async Task InsertAsync(VisitFrequencyPolicy policy, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(policy, cancellationToken: cancellationToken);

    public async Task UpdateAsync(VisitFrequencyPolicy policy, CancellationToken cancellationToken)
    {
        var filter = Builders<VisitFrequencyPolicy>.Filter.Where(p => p.Id == policy.Id && p.TenantId == policy.TenantId);
        await _collection.ReplaceOneAsync(filter, policy, cancellationToken: cancellationToken);
    }
}
