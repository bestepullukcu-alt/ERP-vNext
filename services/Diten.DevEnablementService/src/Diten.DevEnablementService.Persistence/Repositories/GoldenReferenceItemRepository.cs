using Diten.DevEnablementService.Domain.Entities;
using Diten.DevEnablementService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.DevEnablementService.Persistence.Repositories;

public sealed class GoldenReferenceItemRepository : IGoldenReferenceItemRepository
{
    private static readonly (string Code, string Name, string Type, int Priority)[] DefaultSeeds =
    [
        ("GRI-001", "Reference Item A", "Standard", 1),
        ("GRI-002", "Reference Item B", "Custom", 2),
        ("GRI-003", "Reference Item C", "Pro", 3)
    ];

    private readonly IMongoCollection<GoldenReferenceItem> _collection;
    private readonly Guid _tenantId;

    public GoldenReferenceItemRepository(IMongoDatabase database, Application.Common.ITenantContext tenantContext)
    {
        _collection = database.GetCollection<GoldenReferenceItem>("golden_reference_items");
        _tenantId = tenantContext.TenantId;
    }

    public async Task<IReadOnlyList<GoldenReferenceItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);
        return await _collection.Find(TenantFilter()).SortBy(x => x.Code).ToListAsync(cancellationToken);
    }

    public async Task<GoldenReferenceItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<GoldenReferenceItem>.Filter.And(
            TenantFilter(),
            Builders<GoldenReferenceItem>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<GoldenReferenceItem> CreateAsync(GoldenReferenceItem entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;
        await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public async Task<bool> UpdateAsync(GoldenReferenceItem entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<GoldenReferenceItem>.Filter.And(
            TenantFilter(),
            Builders<GoldenReferenceItem>.Filter.Eq(x => x.Id, entity.Id));
        var result = await _collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<GoldenReferenceItem>.Filter.And(
            TenantFilter(),
            Builders<GoldenReferenceItem>.Filter.Eq(x => x.Id, id));

        var update = Builders<GoldenReferenceItem>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<int> BulkDeleteAsync(List<Guid> ids, CancellationToken cancellationToken = default)
    {
        var filter = Builders<GoldenReferenceItem>.Filter.And(
            TenantFilter(),
            Builders<GoldenReferenceItem>.Filter.In(x => x.Id, ids));

        var update = Builders<GoldenReferenceItem>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return (int)result.ModifiedCount;
    }

    private FilterDefinition<GoldenReferenceItem> TenantFilter()
    {
        return Builders<GoldenReferenceItem>.Filter.And(
            Builders<GoldenReferenceItem>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId),
            Builders<GoldenReferenceItem>.Filter.Eq(x => x.IsDeleted, false));
    }

    private async Task EnsureDefaultsAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await _collection.Find(TenantFilter())
            .Project(x => x.Code)
            .ToListAsync(cancellationToken);

        var missing = DefaultSeeds
            .Where(s => !existingCodes.Contains(s.Code))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var seeds = missing.Select(item => new GoldenReferenceItem
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Code = item.Code,
            Name = item.Name,
            ReferenceType = item.Type,
            Priority = item.Priority,
            Description = "Initial seeded reference item for golden template demonstration.",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        }).ToList();

        await _collection.InsertManyAsync(seeds, cancellationToken: cancellationToken);
    }
}
