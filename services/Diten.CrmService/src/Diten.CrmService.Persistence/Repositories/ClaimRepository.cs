using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// SCMM-12 (CAND-CAP-0011) claim persistence. Tenant scoped, soft-delete aware, no delete method (closing is the soft
/// archive lifecycle). The active-code guard excludes archived rows with an <c>ArchivedAt == null</c> equality filter
/// (never <c>$ne</c>, which crash-loops partial indexes). EffectiveFrom / EffectiveTo / ApprovedAt / ArchivedAt
/// (DateTimeOffset → BSON array) are never sorted server-side.
/// </summary>
public sealed class ClaimRepository : IClaimRepository
{
    public const string CollectionName = "claims";

    private readonly IMongoCollection<Claim> _collection;

    public ClaimRepository(IMongoDatabase database)
        => _collection = database.GetCollection<Claim>(CollectionName);

    private static FilterDefinition<Claim> Tenant(Guid tenantId)
        => Builders<Claim>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<Claim?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<Claim>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Claim>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.ClaimCode).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<Claim>> ListByCodeAsync(
        Guid tenantId, string claimCode, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<Claim>.Filter.Eq(x => x.ClaimCode, claimCode))
            .ToListAsync(cancellationToken);
        return rows.OrderByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<Claim?> GetActiveByCodeAsync(
        Guid tenantId, string claimCode, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<Claim>.Filter.Eq(x => x.ClaimCode, claimCode)
                & Builders<Claim>.Filter.Eq(x => x.ArchivedAt, null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task InsertAsync(Claim entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task UpdateAsync(Claim entity, CancellationToken cancellationToken)
        => await _collection.ReplaceOneAsync(
            Builders<Claim>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId),
            entity, cancellationToken: cancellationToken);
}
