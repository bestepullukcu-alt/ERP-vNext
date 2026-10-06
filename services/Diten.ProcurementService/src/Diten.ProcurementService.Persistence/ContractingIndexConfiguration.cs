using Diten.ProcurementService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence;

/// <summary>
/// Contract (contracts) + Clause (clauses) collection index tanımları (MOD-0144 §4). Tenant-first indexing
/// (mongo-indexing.md DB-001): her index TenantId (+ LegalEntityId) ile başlar. ContractId/ClauseId ve
/// Idempotency-Key tenant+LE bazında unique. Clause (Category+Title) tenant+LE bazında unique → 409 DUPLICATE_CLAUSE.
/// </summary>
public static class ContractingIndexConfiguration
{
    public const string ContractCollectionName = "contracts";
    public const string ClauseCollectionName = "clauses";

    public static IReadOnlyList<CreateIndexModel<Contract>> BuildContract()
    {
        var b = Builders<Contract>.IndexKeys;

        return new List<CreateIndexModel<Contract>>
        {
            // Compound tenant+LE — her sorgunun giriş filtresi (COLLSCAN önleme).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId),
                new CreateIndexOptions { Name = "ix_tenant_le" }),

            // ContractId public code — tenant+LE bazında unique.
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ContractId),
                new CreateIndexOptions { Name = "ux_tenant_le_contractid", Unique = true }),

            // Create Idempotency-Key — tenant+LE bazında unique (replay-safe). Sparse: yalnız key taşıyanlar.
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions { Name = "ux_tenant_le_idempotencykey", Unique = true, Sparse = true }),

            // supplierId filtresi (liste).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.SupplierId),
                new CreateIndexOptions { Name = "ix_tenant_le_supplierid" }),

            // rfxId filtresi (award bağı).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.RfxId),
                new CreateIndexOptions { Name = "ix_tenant_le_rfxid", Sparse = true }),

            // Status filtresi (liste + yaşam döngüsü).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_tenant_le_status" })
        };
    }

    public static IReadOnlyList<CreateIndexModel<Clause>> BuildClause()
    {
        var b = Builders<Clause>.IndexKeys;

        return new List<CreateIndexModel<Clause>>
        {
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId),
                new CreateIndexOptions { Name = "ix_tenant_le" }),

            // ClauseId public code — tenant+LE bazında unique.
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ClauseId),
                new CreateIndexOptions { Name = "ux_tenant_le_clauseid", Unique = true }),

            // Duplicate anahtarı: (Category+Title) tenant+LE bazında unique → 409 DUPLICATE_CLAUSE.
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Category).Ascending(x => x.Title),
                new CreateIndexOptions { Name = "ux_tenant_le_category_title", Unique = true }),

            // Create Idempotency-Key — tenant+LE bazında unique (replay-safe). Sparse.
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions { Name = "ux_tenant_le_idempotencykey", Unique = true, Sparse = true }),

            // category filtresi (listClauseLibrary category).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Category),
                new CreateIndexOptions { Name = "ix_tenant_le_category" })
        };
    }

    public static async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var contracts = database.GetCollection<Contract>(ContractCollectionName);
        await contracts.Indexes.CreateManyAsync(BuildContract(), ct);

        var clauses = database.GetCollection<Clause>(ClauseCollectionName);
        await clauses.Indexes.CreateManyAsync(BuildClause(), ct);
    }
}
