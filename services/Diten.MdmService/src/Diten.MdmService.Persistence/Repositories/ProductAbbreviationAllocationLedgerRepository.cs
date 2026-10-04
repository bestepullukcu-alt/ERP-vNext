using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ProductAbbreviationAllocationLedgerRepository
    : IProductAbbreviationAllocationLedgerRepository
{
    public const string CollectionName = "mdm_product_abbreviation_allocation_ledger";
    private readonly IMongoCollection<ProductAbbreviationAllocationLedger> _collection;
    private readonly Guid _tenantId;

    public ProductAbbreviationAllocationLedgerRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _collection = database.GetCollection<ProductAbbreviationAllocationLedger>(CollectionName);
        _tenantId = tenantContext.TenantId;
        EnsureIndexes(_collection);
    }

    public async Task<ProductAbbreviationAllocationLedger?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ProductAbbreviationAllocationLedger? result = await _collection
            .Find(TenantFilter & Builders<ProductAbbreviationAllocationLedger>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);
        return result;
    }

    public async Task<ProductAbbreviationAllocationLedger?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ProductAbbreviationAllocationLedger? result = await _collection.Find(
                TenantFilter & Builders<ProductAbbreviationAllocationLedger>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey))
            .FirstOrDefaultAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// FIX3 item 3 — a code is taken only inside the request's own transaction (the register repository's
    /// RequestEntryAsync). A ledger row already under the same key is the same request only when this holds.
    /// </summary>
    public static bool Matches(
        ProductAbbreviationAllocationLedger persisted,
        ProductAbbreviationAllocationLedger requested)
        => persisted.PayloadHash == requested.PayloadHash
           && persisted.NormalizedAbbreviation == requested.NormalizedAbbreviation
           && persisted.GlobalProductId == requested.GlobalProductId
           && persisted.RegisterEntryId == requested.RegisterEntryId
           // FIX4 item 3: a row left by an earlier attempt is reused only by the person who took it.
           && string.Equals(
               persisted.AllocatedByCanonicalSubjectId,
               requested.AllocatedByCanonicalSubjectId,
               StringComparison.Ordinal);

    public static void EnsureIndexes(IMongoCollection<ProductAbbreviationAllocationLedger> collection)
    {
        collection.Indexes.CreateMany(
        [
            new CreateIndexModel<ProductAbbreviationAllocationLedger>(
                Builders<ProductAbbreviationAllocationLedger>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.NormalizedAbbreviation),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "ux_mdm_product_abbreviation_ledger_tenant_abbreviation"
                }),
            new CreateIndexModel<ProductAbbreviationAllocationLedger>(
                Builders<ProductAbbreviationAllocationLedger>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "ux_mdm_product_abbreviation_ledger_tenant_idempotency"
                })
        ]);
    }

    private FilterDefinition<ProductAbbreviationAllocationLedger> TenantFilter
        => Builders<ProductAbbreviationAllocationLedger>.Filter.Eq(x => x.TenantId, _tenantId)
           & Builders<ProductAbbreviationAllocationLedger>.Filter.Eq(x => x.IsDeleted, false);
}
