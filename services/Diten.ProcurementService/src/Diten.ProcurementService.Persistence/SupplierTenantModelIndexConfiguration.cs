using Diten.ProcurementService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence;

/// <summary>
/// Kiracıda tek tedarikçi modelinin koleksiyonları ve indeksleri (MOD-0140 §4.1, §4.2, §12; DL-2). Her indeks TenantId
/// ile başlar (DB-001). Tekillikler silinmemiş kayıtlarda geçerlidir: göçte birleşen kayıtlar soft delete olarak kalır
/// (MergedIntoId) ve geri açılabilir.
/// </summary>
public static class SupplierTenantModelIndexConfiguration
{
    public const string MasterCollectionName = "supplier_masters";
    public const string RelationshipCollectionName = "supplier_company_relationships";

    public static IReadOnlyList<CreateIndexModel<SupplierMaster>> BuildMasterIndexes()
    {
        var b = Builders<SupplierMaster>.IndexKeys;
        var f = Builders<SupplierMaster>.Filter;

        return new List<CreateIndexModel<SupplierMaster>>
        {
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.SupplierId),
                new CreateIndexOptions<SupplierMaster>
                {
                    Name = "ux_tenant_supplierid",
                    Unique = true,
                    PartialFilterExpression = f.Eq(x => x.IsDeleted, false)
                }),

            // K-2: kiracı + ülke + normalize vergi no. Partial index'te $ne YASAK; `$type: string` boş vergi no'yu dışlar.
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.Country).Ascending(x => x.TaxIdNormalized),
                new CreateIndexOptions<SupplierMaster>
                {
                    Name = "ux_tenant_country_taxid",
                    Unique = true,
                    PartialFilterExpression = f.And(
                        f.Type(x => x.TaxIdNormalized, BsonType.String),
                        f.Eq(x => x.IsDeleted, false))
                }),

            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.MergedIntoId),
                new CreateIndexOptions { Name = "ix_tenant_mergedinto" })
        };
    }

    public static IReadOnlyList<CreateIndexModel<SupplierCompanyRelationship>> BuildRelationshipIndexes()
    {
        var b = Builders<SupplierCompanyRelationship>.IndexKeys;
        var f = Builders<SupplierCompanyRelationship>.Filter;

        return new List<CreateIndexModel<SupplierCompanyRelationship>>
        {
            // Şirket başına tedarikçi için tek ilişki; ikinci bağlama 409 (§12).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.SupplierRef),
                new CreateIndexOptions<SupplierCompanyRelationship>
                {
                    Name = "ux_tenant_le_supplier",
                    Unique = true,
                    PartialFilterExpression = f.Eq(x => x.IsDeleted, false)
                }),

            // (Tenant, LE, eski kod) unique (§12). Dizi alanında `$type: string` en az bir kodu olan kayıtları seçer; boş
            // dizi indekse girmez (girse, boş dizilerin ortak anahtarı tekilliği bozardı).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.LegacySupplierCodes),
                new CreateIndexOptions<SupplierCompanyRelationship>
                {
                    Name = "ux_tenant_le_legacycode",
                    Unique = true,
                    PartialFilterExpression = f.And(
                        f.Type(x => x.LegacySupplierCodes, BsonType.String),
                        f.Eq(x => x.IsDeleted, false))
                }),

            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.SupplierRef),
                new CreateIndexOptions { Name = "ix_tenant_supplierref" })
        };
    }

    public static async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        await database.GetCollection<SupplierMaster>(MasterCollectionName).Indexes.CreateManyAsync(BuildMasterIndexes(), ct);
        await database.GetCollection<SupplierCompanyRelationship>(RelationshipCollectionName).Indexes
            .CreateManyAsync(BuildRelationshipIndexes(), ct);
    }
}
