using Diten.ManufacturingService.Domain.Entities;
using Diten.ManufacturingService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.ManufacturingService.Persistence;

/// <summary>
/// MOD-0193 index'leri (pack §4). İkisi kural taşır: item başına TEK Effective sürüm (kısmi eşsiz) ve item başına
/// eşsiz revizyon numarası. Diğerleri kiracı + LE kapsamlı okuma yollarıdır.
/// </summary>
public static class BomIndexConfiguration
{
    public static async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken ct)
    {
        var boms = database.GetCollection<BomVersion>(BomCollections.Boms);
        var keys = Builders<BomVersion>.IndexKeys;
        await boms.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BomVersion>(
                keys.Ascending(b => b.TenantId).Ascending(b => b.LegalEntityId).Ascending(b => b.ItemId).Ascending(b => b.RevisionNo),
                new CreateIndexOptions { Name = "ux_scope_item_revision", Unique = true }),
            new CreateIndexModel<BomVersion>(
                keys.Ascending(b => b.TenantId).Ascending(b => b.LegalEntityId).Ascending(b => b.ItemId),
                new CreateIndexOptions<BomVersion>
                {
                    Name = "ux_scope_item_single_effective",
                    Unique = true,
                    PartialFilterExpression = new BsonDocument { { "Status", BomStatus.Effective.ToString() }, { "IsDeleted", false } }
                }),
            new CreateIndexModel<BomVersion>(
                keys.Ascending(b => b.TenantId).Ascending(b => b.LegalEntityId).Ascending(b => b.IsDeleted).Descending(b => b.UpdatedAt),
                new CreateIndexOptions { Name = "ix_scope_list" })
        ], ct);

        var history = database.GetCollection<BomHistoryEntry>(BomCollections.History);
        await history.Indexes.CreateOneAsync(new CreateIndexModel<BomHistoryEntry>(
            Builders<BomHistoryEntry>.IndexKeys.Ascending(h => h.TenantId).Ascending(h => h.LegalEntityId).Ascending(h => h.BomVersionId).Ascending(h => h.OccurredAtUtc),
            new CreateIndexOptions { Name = "ix_scope_version_time" }), cancellationToken: ct);
    }
}
