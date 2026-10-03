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
        /*
         * BL-500 — the tenant of the write is the ROW's own tenant, the same rule CreateAsync follows.
         *
         * This filter used to read TenantContext.TenantId. A platform administrator's request runs in the platform
         * context, where that is Guid.Empty: the filter matched no row, MatchedCount was 0, and every suspend, enable
         * and expiry change made from the tenant's Modules tab was refused as "modified by another process" — a
         * concurrency error about a row nobody had touched. The handlers load the row by (route tenant, id), so the
         * row already carries the authoritative tenant.
         *
         * The tenant condition is NOT dropped — only where its value comes from changes. The filter still pins tenant
         * + id + (when given) version, and a tenant-scoped caller is still refused a row of another tenant before any
         * write is attempted.
         */
        if (!TenantContext.IsPlatformContext && entitlement.TenantId != TenantContext.TenantId)
        {
            throw new TenantModuleEntitlementConcurrencyException();
        }

        var filters = new List<FilterDefinition<TenantModuleEntitlement>>
        {
            ExecutionFilter,
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.TenantId, entitlement.TenantId),
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
        // BL-500 — the same guard as CreateAsync and UpdateAsync: a tenant-scoped caller is pinned to its own tenant
        // BEFORE any write is attempted; a platform actor works on the tenant its route names. Without it this member
        // trusted whatever tenant id it was handed.
        if (!TenantContext.IsPlatformContext && tenantId != TenantContext.TenantId)
        {
            throw new TenantModuleEntitlementConcurrencyException();
        }

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
        // Platform actors (PlatformActor policy) operate cross-tenant: the middleware sets the platform
        // context (TenantContext.TenantId == Guid.Empty) and the authoritative target tenant is carried on
        // the entity itself (set by the handler from the route's {tenantId}). Only a genuine tenant-scoped
        // caller must be pinned to its own tenant — comparing a platform actor's Guid.Empty context against a
        // real target tenant would (and did) reject every manual entitlement add with a bogus concurrency error.
        if (!TenantContext.IsPlatformContext && entity.TenantId != TenantContext.TenantId)
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

    /// <summary>
    /// BL-500 — the base repository's delete-by-id: no tenant, no version, no transaction. Nothing calls it (the
    /// interface does not expose it), and it is closed for the same reason as the three members above: an
    /// entitlement is removed through <see cref="SoftDeleteAsync(IPlatformTransactionSession, Guid, Guid, byte[], CancellationToken)"/> or not at all.
    /// </summary>
    public override Task DeleteAsync(Guid id, CancellationToken ct = default) =>
        throw new PlatformTransactionUnavailableException(
            "Sessionless physical-entitlement mutation is disabled until the caller supplies the Platform transaction session.");

    private static string NormalizeModuleCode(string moduleCode) => moduleCode.Trim().ToUpperInvariant();
}
