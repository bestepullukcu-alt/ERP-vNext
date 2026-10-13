using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

// WP-VW-W4 (W4-BE-c, §2.2) — the doctor insights stores: tenant scoped, soft-delete aware, optimistic replace on the
// technical version (the VisitEvidenceException pattern). The histories are append-only and ordered newest first IN
// MEMORY (At / CreatedAt are DateTimeOffset BSON arrays — never a server-side sort key).

public sealed class DoctorProfileRepository : IDoctorProfileRepository
{
    public const string CollectionName = "doctor_profiles";

    private readonly IMongoCollection<DoctorProfile> _collection;

    public DoctorProfileRepository(IMongoDatabase database)
        => _collection = database.GetCollection<DoctorProfile>(CollectionName);

    private static FilterDefinition<DoctorProfile> Tenant(Guid tenantId)
        => Builders<DoctorProfile>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<DoctorProfile?> GetByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<DoctorProfile>.Filter.Eq(x => x.ContactId, contactId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<DoctorProfile>> ListByContactsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> contactIds, CancellationToken cancellationToken)
        => contactIds.Count == 0
            ? Array.Empty<DoctorProfile>()
            : await _collection.Find(Tenant(tenantId) & Builders<DoctorProfile>.Filter.In(x => x.ContactId, contactIds))
                .ToListAsync(cancellationToken);

    public Task InsertAsync(DoctorProfile entity, CancellationToken cancellationToken)
        => _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<bool> ReplaceAsync(DoctorProfile entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<DoctorProfile>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        if (result.IsAcknowledged && result.MatchedCount == 1) return true;
        entity.Version = expectedVersion;
        return false;
    }
}

public sealed class DoctorProfileChangeRepository : IDoctorProfileChangeRepository
{
    public const string CollectionName = "doctor_profile_history";

    private readonly IMongoCollection<DoctorProfileChange> _collection;

    public DoctorProfileChangeRepository(IMongoDatabase database)
        => _collection = database.GetCollection<DoctorProfileChange>(CollectionName);

    public Task InsertManyAsync(IReadOnlyCollection<DoctorProfileChange> rows, CancellationToken cancellationToken)
        => rows.Count == 0 ? Task.CompletedTask : _collection.InsertManyAsync(rows, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<DoctorProfileChange>> ListByContactAsync(Guid tenantId, Guid contactId, int take, CancellationToken cancellationToken)
        => (await _collection.Find(Builders<DoctorProfileChange>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted && x.ContactId == contactId))
                .ToListAsync(cancellationToken))
            .OrderByDescending(x => x.At).ThenByDescending(x => x.CreatedAt).Take(take).ToList();
}

public sealed class DoctorProductPotentialRepository : IDoctorProductPotentialRepository
{
    public const string CollectionName = "doctor_product_potentials";

    private readonly IMongoCollection<DoctorProductPotential> _collection;

    public DoctorProductPotentialRepository(IMongoDatabase database)
        => _collection = database.GetCollection<DoctorProductPotential>(CollectionName);

    private static FilterDefinition<DoctorProductPotential> Tenant(Guid tenantId)
        => Builders<DoctorProductPotential>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<DoctorProductPotential?> GetAsync(Guid tenantId, Guid contactId, Guid productId, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId)
                                  & Builders<DoctorProductPotential>.Filter.Eq(x => x.ContactId, contactId)
                                  & Builders<DoctorProductPotential>.Filter.Eq(x => x.GlobalProductId, productId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<DoctorProductPotential>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<DoctorProductPotential>.Filter.Eq(x => x.ContactId, contactId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DoctorProductPotential>> ListByContactsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> contactIds, Guid? productId, CancellationToken cancellationToken)
    {
        if (contactIds.Count == 0) return Array.Empty<DoctorProductPotential>();
        var filter = Tenant(tenantId) & Builders<DoctorProductPotential>.Filter.In(x => x.ContactId, contactIds);
        if (productId is { } p) filter &= Builders<DoctorProductPotential>.Filter.Eq(x => x.GlobalProductId, p);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DoctorProductPotential>> ListAsync(Guid tenantId, Guid? productId, CancellationToken cancellationToken)
    {
        var filter = Tenant(tenantId);
        if (productId is { } p) filter &= Builders<DoctorProductPotential>.Filter.Eq(x => x.GlobalProductId, p);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public Task InsertAsync(DoctorProductPotential entity, CancellationToken cancellationToken)
        => _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<bool> ReplaceAsync(DoctorProductPotential entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<DoctorProductPotential>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        if (result.IsAcknowledged && result.MatchedCount == 1) return true;
        entity.Version = expectedVersion;
        return false;
    }
}

public sealed class DoctorProductPotentialChangeRepository : IDoctorProductPotentialChangeRepository
{
    public const string CollectionName = "doctor_product_potential_history";

    private readonly IMongoCollection<DoctorProductPotentialChange> _collection;

    public DoctorProductPotentialChangeRepository(IMongoDatabase database)
        => _collection = database.GetCollection<DoctorProductPotentialChange>(CollectionName);

    public Task InsertManyAsync(IReadOnlyCollection<DoctorProductPotentialChange> rows, CancellationToken cancellationToken)
        => rows.Count == 0 ? Task.CompletedTask : _collection.InsertManyAsync(rows, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<DoctorProductPotentialChange>> ListByContactAsync(Guid tenantId, Guid contactId, int take, CancellationToken cancellationToken)
        => (await _collection.Find(Builders<DoctorProductPotentialChange>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted && x.ContactId == contactId))
                .ToListAsync(cancellationToken))
            .OrderByDescending(x => x.At).ThenByDescending(x => x.CreatedAt).Take(take).ToList();
}

public sealed class ProductPotentialThresholdRepository : IProductPotentialThresholdRepository
{
    public const string CollectionName = "product_potential_thresholds";

    private readonly IMongoCollection<ProductPotentialThreshold> _collection;

    public ProductPotentialThresholdRepository(IMongoDatabase database)
        => _collection = database.GetCollection<ProductPotentialThreshold>(CollectionName);

    private static FilterDefinition<ProductPotentialThreshold> Tenant(Guid tenantId)
        => Builders<ProductPotentialThreshold>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<IReadOnlyList<ProductPotentialThreshold>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);

    public async Task<ProductPotentialThreshold?> GetAsync(Guid tenantId, Guid? productId, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<ProductPotentialThreshold>.Filter.Eq(x => x.GlobalProductId, productId))
            .FirstOrDefaultAsync(cancellationToken);

    public Task InsertAsync(ProductPotentialThreshold entity, CancellationToken cancellationToken)
        => _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<bool> ReplaceAsync(ProductPotentialThreshold entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<ProductPotentialThreshold>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        if (result.IsAcknowledged && result.MatchedCount == 1) return true;
        entity.Version = expectedVersion;
        return false;
    }
}

public sealed class ProductPotentialThresholdChangeRepository : IProductPotentialThresholdChangeRepository
{
    public const string CollectionName = "product_potential_threshold_history";

    private readonly IMongoCollection<ProductPotentialThresholdChange> _collection;

    public ProductPotentialThresholdChangeRepository(IMongoDatabase database)
        => _collection = database.GetCollection<ProductPotentialThresholdChange>(CollectionName);

    public Task InsertManyAsync(IReadOnlyCollection<ProductPotentialThresholdChange> rows, CancellationToken cancellationToken)
        => rows.Count == 0 ? Task.CompletedTask : _collection.InsertManyAsync(rows, cancellationToken: cancellationToken);

    public async Task<(IReadOnlyList<ProductPotentialThresholdChange> Items, long Total)> ListAsync(
        Guid tenantId, int skip, int take, CancellationToken cancellationToken)
    {
        var rows = (await _collection.Find(Builders<ProductPotentialThresholdChange>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted))
                .ToListAsync(cancellationToken))
            .OrderByDescending(x => x.At).ThenByDescending(x => x.CreatedAt).ToList();
        return (rows.Skip(skip).Take(take).ToList(), rows.Count);
    }
}
