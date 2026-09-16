using Diten.ProcurementService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence;

/// <summary>
/// Supplier collection index tanımları (MOD-0140 §4). Tenant-first indexing (mongo-indexing.md DB-001):
/// her index TenantId (+ LegalEntityId) ile başlar.
/// </summary>
public static class SupplierIndexConfiguration
{
    public const string CollectionName = "suppliers";

    public static IReadOnlyList<CreateIndexModel<Supplier>> Build()
    {
        var b = Builders<Supplier>.IndexKeys;

        return new List<CreateIndexModel<Supplier>>
        {
            // Compound tenant+LE — her sorgunun giriş filtresi (COLLSCAN önleme).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId),
                new CreateIndexOptions { Name = "ix_tenant_le" }),

            // SupplierId public code — tenant+LE bazında unique.
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.SupplierId),
                new CreateIndexOptions { Name = "ux_tenant_le_supplierid", Unique = true }),

            // TaxId — tenant+LE bazında partial-unique (yalnız dolu ve silinmemiş kayıtlarda).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.TaxId),
                new CreateIndexOptions<Supplier>
                {
                    Name = "ux_tenant_le_taxid",
                    Unique = true,
                    PartialFilterExpression = Builders<Supplier>.Filter.And(
                        Builders<Supplier>.Filter.Exists(x => x.TaxId),
                        Builders<Supplier>.Filter.Ne(x => x.TaxId, null),
                        Builders<Supplier>.Filter.Eq(x => x.IsDeleted, false))
                }),

            // Name text index (arama).
            new(
                b.Text(x => x.Name),
                new CreateIndexOptions { Name = "tx_name" })
        };
    }

    public static async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var collection = database.GetCollection<Supplier>(CollectionName);
        await collection.Indexes.CreateManyAsync(Build(), ct);
    }
}
