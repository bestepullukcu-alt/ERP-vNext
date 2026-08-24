using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ProductAbbreviationHistoryRepository : IProductAbbreviationHistoryRepository
{
    private readonly IMongoCollection<ProductAbbreviationHistoryEntry> _collection;
    private readonly Guid _tenantId;

    public ProductAbbreviationHistoryRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _collection = database.GetCollection<ProductAbbreviationHistoryEntry>("mdm_product_abbreviation_history");
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<bool> AppendIfAbsentAsync(
        ProductAbbreviationHistoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        entry.TenantId = _tenantId;
        entry.IsDeleted = false;
        entry.CreatedAt = entry.OccurredAtUtc;
        entry.UpdatedAt = entry.OccurredAtUtc;
        entry.Version = 0;
        try
        {
            await _collection.InsertOneAsync(entry, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await _collection.Find(
                    TenantFilter
                    & Builders<ProductAbbreviationHistoryEntry>.Filter.Eq(x => x.IdempotencyKey, entry.IdempotencyKey)
                    & Builders<ProductAbbreviationHistoryEntry>.Filter.Eq(x => x.EventType, entry.EventType))
                .FirstOrDefaultAsync(cancellationToken);
            return existing is not null
                   && existing.RegisterEntryId == entry.RegisterEntryId
                   && existing.EvidenceHash == entry.EvidenceHash;
        }
    }

    public async Task<IReadOnlyList<ProductAbbreviationHistoryEntry>> GetForRegisterEntryAsync(
        Guid registerEntryId,
        CancellationToken cancellationToken = default)
        => await _collection.Find(
                TenantFilter & Builders<ProductAbbreviationHistoryEntry>.Filter.Eq(
                    x => x.RegisterEntryId,
                    registerEntryId))
            .SortBy(x => x.OccurredAtUtc)
            .ToListAsync(cancellationToken);

    private void EnsureIndexes()
    {
        _collection.Indexes.CreateMany(
        [
            new CreateIndexModel<ProductAbbreviationHistoryEntry>(
                Builders<ProductAbbreviationHistoryEntry>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IdempotencyKey)
                    .Ascending(x => x.EventType),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "ux_mdm_product_abbreviation_history_tenant_event"
                }),
            new CreateIndexModel<ProductAbbreviationHistoryEntry>(
                Builders<ProductAbbreviationHistoryEntry>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.RegisterEntryId)
                    .Ascending(x => x.OccurredAtUtc),
                new CreateIndexOptions { Name = "ix_mdm_product_abbreviation_history_tenant_entry" })
        ]);
    }

    private FilterDefinition<ProductAbbreviationHistoryEntry> TenantFilter
        => Builders<ProductAbbreviationHistoryEntry>.Filter.Eq(x => x.TenantId, _tenantId)
           & Builders<ProductAbbreviationHistoryEntry>.Filter.Eq(x => x.IsDeleted, false);
}
