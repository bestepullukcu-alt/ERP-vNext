using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU07 — tenant-scoped Mongo repositories for the identifier allocation ledger + atomic sequence counter.

public sealed class DocumentIdentifierAllocationRepository
    : TenantRepository<DocumentIdentifierAllocation>, IDocumentIdentifierAllocationRepository
{
    public DocumentIdentifierAllocationRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementIdentifierAllocations) { }

    public new Task<DocumentIdentifierAllocation> CreateAsync(DocumentIdentifierAllocation allocation, CancellationToken ct = default) =>
        base.CreateAsync(allocation, ct);

    public async Task<bool> ExistsValueIncludingDeletedAsync(DocumentIdentifierType type, string identifierValue, CancellationToken ct = default)
    {
        // Deliberately DOES NOT use ExecutionFilter (which excludes IsDeleted). Never-reuse must see soft-deleted rows.
        var filter = Builders<DocumentIdentifierAllocation>.Filter.And(
            Builders<DocumentIdentifierAllocation>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<DocumentIdentifierAllocation>.Filter.Eq(x => x.IdentifierType, type),
            Builders<DocumentIdentifierAllocation>.Filter.Eq(x => x.IdentifierValue, identifierValue));
        return await Collection.Find(filter).Limit(1).AnyAsync(ct);
    }

    public async Task<IReadOnlyList<DocumentIdentifierAllocation>> ListAsync(IdentifierAllocationListFilter filter, CancellationToken ct = default)
    {
        var f = Builders<DocumentIdentifierAllocation>.Filter;
        var conditions = new List<FilterDefinition<DocumentIdentifierAllocation>> { ExecutionFilter };
        if (filter.IdentifierType is { } t) conditions.Add(f.Eq(x => x.IdentifierType, t));
        if (filter.AllocationStatus is { } s) conditions.Add(f.Eq(x => x.AllocationStatus, s));
        if (filter.RegisterEntryId is { } r) conditions.Add(f.Eq(x => x.RegisterEntryId, r));
        return await Collection.Find(f.And(conditions)).SortByDescending(x => x.AllocatedAt).ToListAsync(ct);
    }

    public async Task<bool> UpdateAsync(DocumentIdentifierAllocation allocation, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentIdentifierAllocation>.Filter.And(ExecutionFilter,
                Builders<DocumentIdentifierAllocation>.Filter.Eq(x => x.Id, allocation.Id)),
            allocation, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentIdentifierSequenceCounterRepository
    : TenantRepository<DocumentIdentifierSequenceCounter>, IDocumentIdentifierSequenceCounterRepository
{
    public DocumentIdentifierSequenceCounterRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementIdentifierSequenceCounters) { }

    public async Task<long> NextAsync(DocumentIdentifierType type, string? prefix, string? domainCode, string? typeCode, string createdBy, CancellationToken ct = default)
    {
        var tenantId = TenantContext.TenantId;
        var filter = Builders<DocumentIdentifierSequenceCounter>.Filter.And(
            Builders<DocumentIdentifierSequenceCounter>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<DocumentIdentifierSequenceCounter>.Filter.Eq(x => x.IdentifierType, type),
            Builders<DocumentIdentifierSequenceCounter>.Filter.Eq(x => x.Prefix, prefix),
            Builders<DocumentIdentifierSequenceCounter>.Filter.Eq(x => x.DomainCode, domainCode),
            Builders<DocumentIdentifierSequenceCounter>.Filter.Eq(x => x.TypeCode, typeCode));

        // Atomic $inc with upsert. $setOnInsert seeds the identity fields; NextNumber is created by $inc (0 → 1).
        var update = Builders<DocumentIdentifierSequenceCounter>.Update
            .Inc(x => x.NextNumber, 1L)
            .SetOnInsert(x => x.Id, Guid.NewGuid())
            .SetOnInsert(x => x.TenantId, tenantId)
            .SetOnInsert(x => x.IdentifierType, type)
            .SetOnInsert(x => x.Prefix, prefix)
            .SetOnInsert(x => x.DomainCode, domainCode)
            .SetOnInsert(x => x.TypeCode, typeCode)
            .SetOnInsert(x => x.CreatedAt, DateTimeOffset.UtcNow)
            .SetOnInsert(x => x.CreatedBy, createdBy)
            .SetOnInsert(x => x.IsDeleted, false)
            .SetOnInsert(x => x.Version, 1);

        var options = new FindOneAndUpdateOptions<DocumentIdentifierSequenceCounter>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var counter = await Collection.FindOneAndUpdateAsync(filter, update, options, ct);
        return counter.NextNumber;
    }
}
