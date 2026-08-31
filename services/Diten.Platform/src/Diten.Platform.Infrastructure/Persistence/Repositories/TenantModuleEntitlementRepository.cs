using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class TenantModuleEntitlementRepository : GlobalRepository<TenantModuleEntitlement>, ITenantModuleEntitlementRepository
{
    private readonly IPlatformDbContext _dbContext;

    public TenantModuleEntitlementRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.TenantModuleEntitlements)
    {
        _dbContext = dbContext;
    }

    public async Task<TenantModuleEntitlement?> GetByIdAsync(Guid tenantId, Guid entitlementId, CancellationToken ct = default)
    {
        var filter = Builders<TenantModuleEntitlement>.Filter.And(
            ExecutionFilter,
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.Id, entitlementId));

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TenantModuleEntitlement>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        var filter = Builders<TenantModuleEntitlement>.Filter.And(
            ExecutionFilter,
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.TenantId, tenantId));

        return await Collection.Find(filter)
            .Sort(Builders<TenantModuleEntitlement>.Sort.Ascending(x => x.ModuleCode).Ascending(x => x.Source))
            .ToListAsync(ct);
    }

    public Task<long> CountEnabledAsync(
        IPlatformTransactionSession session,
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        }

        var filter = Builders<TenantModuleEntitlement>.Filter.And(
            ExecutionFilter,
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.IsEnabled, true));

        return Collection.CountDocumentsAsync(
            PlatformMongoTransactionSession.Require(session, _dbContext),
            filter,
            cancellationToken: ct);
    }

    public async Task<IReadOnlyList<TenantModuleEntitlement>> GetByTenantAndModuleAsync(Guid tenantId, string moduleCode, CancellationToken ct = default)
    {
        var normalizedCode = NormalizeModuleCode(moduleCode);
        var filter = Builders<TenantModuleEntitlement>.Filter.And(
            ExecutionFilter,
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.ModuleCode, normalizedCode));

        return await Collection.Find(filter)
            .Sort(Builders<TenantModuleEntitlement>.Sort.Ascending(x => x.Source))
            .ToListAsync(ct);
    }

    public async Task<TenantModuleEntitlement?> GetActiveBySourceAsync(
        Guid tenantId,
        string moduleCode,
        EntitlementSource source,
        Guid? excludeId = null,
        CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<TenantModuleEntitlement>>
        {
            ExecutionFilter,
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.ModuleCode, NormalizeModuleCode(moduleCode)),
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.Source, source)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<TenantModuleEntitlement>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return await Collection.Find(Builders<TenantModuleEntitlement>.Filter.And(filters)).FirstOrDefaultAsync(ct);
    }

    public async Task UpdateAsync(IPlatformTransactionSession session, TenantModuleEntitlement entitlement, byte[]? expectedRowVersion, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<TenantModuleEntitlement>>
        {
            ExecutionFilter,
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.Id, entitlement.Id)
        };

        if (expectedRowVersion is { Length: > 0 })
        {
            filters.Add(Builders<TenantModuleEntitlement>.Filter.Eq(x => x.RowVersion, expectedRowVersion));
        }

        entitlement.ModuleCode = NormalizeModuleCode(entitlement.ModuleCode);
        entitlement.UpdatedAt = DateTimeOffset.UtcNow;
        entitlement.RowVersion = Guid.NewGuid().ToByteArray();

        var result = await Collection.ReplaceOneAsync(
            PlatformMongoTransactionSession.Require(session, _dbContext),
            Builders<TenantModuleEntitlement>.Filter.And(filters),
            entitlement,
            cancellationToken: ct);

        if (result.MatchedCount == 0)
        {
            throw new TenantModuleEntitlementConcurrencyException();
        }
    }

    public async Task SoftDeleteAsync(IPlatformTransactionSession session, Guid tenantId, Guid entitlementId, byte[]? expectedRowVersion, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<TenantModuleEntitlement>>
        {
            ExecutionFilter,
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.Id, entitlementId)
        };

        if (expectedRowVersion is { Length: > 0 })
        {
            filters.Add(Builders<TenantModuleEntitlement>.Filter.Eq(x => x.RowVersion, expectedRowVersion));
        }

        var update = Builders<TenantModuleEntitlement>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Set(x => x.RowVersion, Guid.NewGuid().ToByteArray());

        var result = await Collection.UpdateOneAsync(
            PlatformMongoTransactionSession.Require(session, _dbContext),
            Builders<TenantModuleEntitlement>.Filter.And(filters),
            update,
            cancellationToken: ct);
        if (result.MatchedCount == 0)
        {
            throw new TenantModuleEntitlementConcurrencyException();
        }
    }

    public async Task<TenantModuleEntitlement> CreateAsync(
        IPlatformTransactionSession session,
        TenantModuleEntitlement entity,
        CancellationToken ct = default)
    {
        if (entity.TenantId != TenantContext.TenantId)
        {
            throw new TenantModuleEntitlementConcurrencyException();
        }

        entity.ModuleCode = NormalizeModuleCode(entity.ModuleCode);
        entity.IsDeleted = false;
        await Collection.InsertOneAsync(
            PlatformMongoTransactionSession.Require(session, _dbContext),
            entity,
            cancellationToken: ct);
        return entity;
    }

    [Obsolete("Authoritative entitlement mutations require an explicit Platform transaction session.")]
    public override Task<TenantModuleEntitlement> CreateAsync(TenantModuleEntitlement entity, CancellationToken ct = default) =>
        throw new PlatformTransactionUnavailableException(
            "Sessionless physical-entitlement mutation is disabled until the caller supplies the Platform transaction session.");

    [Obsolete("Authoritative entitlement mutations require an explicit Platform transaction session.")]
    public Task UpdateAsync(TenantModuleEntitlement entitlement, byte[]? expectedRowVersion, CancellationToken ct = default) =>
        throw new PlatformTransactionUnavailableException(
            "Sessionless physical-entitlement mutation is disabled until the caller supplies the Platform transaction session.");

    [Obsolete("Authoritative entitlement mutations require an explicit Platform transaction session.")]
    public Task SoftDeleteAsync(Guid tenantId, Guid entitlementId, byte[]? expectedRowVersion, CancellationToken ct = default) =>
        throw new PlatformTransactionUnavailableException(
            "Sessionless physical-entitlement mutation is disabled until the caller supplies the Platform transaction session.");

    private static string NormalizeModuleCode(string moduleCode) => moduleCode.Trim().ToUpperInvariant();
}
