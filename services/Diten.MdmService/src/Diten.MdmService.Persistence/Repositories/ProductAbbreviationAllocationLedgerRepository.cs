using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ProductAbbreviationAllocationLedgerRepository
    : IProductAbbreviationAllocationLedgerRepository
{
    private readonly IMongoCollection<ProductAbbreviationAllocationLedger> _collection;
    private readonly Guid _tenantId;

    public ProductAbbreviationAllocationLedgerRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _collection = database.GetCollection<ProductAbbreviationAllocationLedger>("mdm_product_abbreviation_allocation_ledger");
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
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

    public async Task<ProductAbbreviationAllocationResult> AllocateAsync(
        ProductAbbreviationAllocationLedger allocation,
        CancellationToken cancellationToken = default)
    {
        var replay = await GetByIdempotencyKeyAsync(allocation.IdempotencyKey, cancellationToken);
        if (replay is not null)
        {
            return Matches(replay, allocation)
                ? new(true, replay, IsReplay: true)
                : new(false, replay, "ABBREVIATION_IDEMPOTENCY_CONFLICT");
        }

        var alreadyAllocated = await _collection.Find(
                TenantFilter & Builders<ProductAbbreviationAllocationLedger>.Filter.Eq(
                    x => x.NormalizedAbbreviation,
                    allocation.NormalizedAbbreviation))
            .FirstOrDefaultAsync(cancellationToken);
        if (alreadyAllocated is not null)
        {
            return new(false, alreadyAllocated, "ABBREVIATION_ALREADY_ALLOCATED");
        }

        allocation.TenantId = _tenantId;
        allocation.IsDeleted = false;
        allocation.DeletedAt = null;
        allocation.CreatedAt = allocation.AllocatedAtUtc;
        allocation.UpdatedAt = allocation.AllocatedAtUtc;
        allocation.Version = 0;

        try
        {
            await _collection.InsertOneAsync(allocation, cancellationToken: cancellationToken);
            return new(true, allocation);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            replay = await GetByIdempotencyKeyAsync(allocation.IdempotencyKey, cancellationToken);
            if (replay is not null)
            {
                return Matches(replay, allocation)
                    ? new(true, replay, IsReplay: true)
                    : new(false, replay, "ABBREVIATION_IDEMPOTENCY_CONFLICT");
            }

            alreadyAllocated = await _collection.Find(
                    TenantFilter & Builders<ProductAbbreviationAllocationLedger>.Filter.Eq(
                        x => x.NormalizedAbbreviation,
                        allocation.NormalizedAbbreviation))
                .FirstOrDefaultAsync(cancellationToken);
            return new(false, alreadyAllocated, "ABBREVIATION_ALREADY_ALLOCATED");
        }
    }

    private static bool Matches(
        ProductAbbreviationAllocationLedger persisted,
        ProductAbbreviationAllocationLedger requested)
        => persisted.PayloadHash == requested.PayloadHash
           && persisted.NormalizedAbbreviation == requested.NormalizedAbbreviation
           && persisted.GlobalProductId == requested.GlobalProductId
           && persisted.RegisterEntryId == requested.RegisterEntryId;

    private void EnsureIndexes()
    {
        _collection.Indexes.CreateMany(
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
