using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// WP-CL-BE-1 (claims v2) claim country-version persistence (<c>claim_country_versions</c>). Tenant scoped,
/// soft-delete aware, no delete method. ClaimId is a string-Guid (class-mapped) so the by-claim filter matches.
/// ValidFrom / ValidTo / ApprovedAt / ArchivedAt (DateTimeOffset → BSON array) are never sorted server-side.
/// </summary>
public sealed class ClaimCountryVersionRepository : IClaimCountryVersionRepository
{
    public const string CollectionName = "claim_country_versions";

    private readonly IMongoCollection<ClaimCountryVersion> _collection;

    public ClaimCountryVersionRepository(IMongoDatabase database)
        => _collection = database.GetCollection<ClaimCountryVersion>(CollectionName);

    private static FilterDefinition<ClaimCountryVersion> Tenant(Guid tenantId)
        => Builders<ClaimCountryVersion>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<ClaimCountryVersion?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<ClaimCountryVersion>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimCodeAsync(
        Guid tenantId, string claimCode, CancellationToken cancellationToken)
        => Order(await _collection
            .Find(Tenant(tenantId) & Builders<ClaimCountryVersion>.Filter.Eq(x => x.ClaimCode, claimCode))
            .ToListAsync(cancellationToken));

    public async Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimIdAsync(
        Guid tenantId, Guid claimId, CancellationToken cancellationToken)
        => Order(await _collection
            .Find(Tenant(tenantId) & Builders<ClaimCountryVersion>.Filter.Eq(x => x.ClaimId, claimId))
            .ToListAsync(cancellationToken));

    public async Task<IReadOnlyList<ClaimCountryVersion>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
        => Order(await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken));

    public async Task InsertAsync(ClaimCountryVersion entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task UpdateAsync(ClaimCountryVersion entity, CancellationToken cancellationToken)
        => await _collection.ReplaceOneAsync(
            Builders<ClaimCountryVersion>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId),
            entity, cancellationToken: cancellationToken);

    // In-memory order (DateTimeOffset is a BSON array — never a server-side sort key).
    private static IReadOnlyList<ClaimCountryVersion> Order(List<ClaimCountryVersion> rows)
        => rows.OrderBy(x => x.ClaimCode).ThenBy(x => x.CountryCode).ThenByDescending(x => x.CreatedAt).ToList();
}
