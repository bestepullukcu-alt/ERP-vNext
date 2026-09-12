using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// SCMM-11 (CAND-CAP-0011) eligibility policy persistence. Same rules as the concept-graph repositories: tenant scoped,
/// soft-delete aware, no delete method (closing is the soft archive lifecycle). The active-code guard excludes archived
/// rows with an <c>ArchivedAt == null</c> equality filter (never <c>$ne</c>, which crash-loops partial indexes).
/// EffectiveFrom / EffectiveTo / ArchivedAt (DateTimeOffset → BSON array) are never sorted server-side.
/// </summary>
public sealed class EligibilityPolicyRepository : IEligibilityPolicyRepository
{
    public const string CollectionName = "eligibility_policies";

    private readonly IMongoCollection<EligibilityPolicy> _collection;

    public EligibilityPolicyRepository(IMongoDatabase database)
        => _collection = database.GetCollection<EligibilityPolicy>(CollectionName);

    private static FilterDefinition<EligibilityPolicy> Tenant(Guid tenantId)
        => Builders<EligibilityPolicy>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<EligibilityPolicy?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<EligibilityPolicy>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<EligibilityPolicy>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.PolicyCode).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<EligibilityPolicy>> ListByCodeAsync(
        Guid tenantId, string policyCode, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<EligibilityPolicy>.Filter.Eq(x => x.PolicyCode, policyCode))
            .ToListAsync(cancellationToken);
        return rows.OrderByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<EligibilityPolicy?> GetActiveByCodeAsync(
        Guid tenantId, string policyCode, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<EligibilityPolicy>.Filter.Eq(x => x.PolicyCode, policyCode)
                & Builders<EligibilityPolicy>.Filter.Eq(x => x.ArchivedAt, null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task InsertAsync(EligibilityPolicy entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task UpdateAsync(EligibilityPolicy entity, CancellationToken cancellationToken)
        => await _collection.ReplaceOneAsync(
            Builders<EligibilityPolicy>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId),
            entity, cancellationToken: cancellationToken);
}
