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
         * The tenant condition is NOT dropped — only where its value comes from changes. The filter pins tenant + id +
         * version, and a tenant-scoped caller is refused a row of another tenant before any write is attempted.
         */
        if (!TenantContext.IsPlatformContext && entitlement.TenantId != TenantContext.TenantId)
        {
            throw new TenantModuleEntitlementTenantMismatchException();
        }

        RequireRowVersion(expectedRowVersion);
        var filters = new List<FilterDefinition<TenantModuleEntitlement>>
        {
            ExecutionFilter,
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.TenantId, entitlement.TenantId),
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.Id, entitlement.Id),
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.RowVersion, expectedRowVersion)
        };

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
        // BEFORE any write is attempted. A PLATFORM actor has no tenant of its own to be pinned to: for it this member
        // still writes under whatever tenant id it is handed, and that id must be the route's — the handler loads the
        // row by (route tenant, id) first, and the filter below matches nothing under any other tenant.
        if (!TenantContext.IsPlatformContext && tenantId != TenantContext.TenantId)
        {
            throw new TenantModuleEntitlementTenantMismatchException();
        }

        RequireRowVersion(expectedRowVersion);
        var filters = new List<FilterDefinition<TenantModuleEntitlement>>
        {
            ExecutionFilter,
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.Id, entitlementId),
            Builders<TenantModuleEntitlement>.Filter.Eq(x => x.RowVersion, expectedRowVersion)
        };

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
            throw new TenantModuleEntitlementTenantMismatchException();
        }

        entity.ModuleCode = NormalizeModuleCode(entity.ModuleCode);
        entity.IsDeleted = false;
        try
        {
            await Collection.InsertOneAsync(
                PlatformMongoTransactionSession.Require(session, _dbContext),
                entity,
                cancellationToken: ct);
        }
        catch (MongoException exception) when (IsConcurrentCreate(exception))
        {
            // BL-500 — one live row per (tenant, module, source): ux_tenant_module_entitlements_active_source. Another
            // writer of the same row (two screens suspending the same plan module at once) is a stale screen, not a
            // server failure. It surfaces in two ways: the other row is committed (duplicate key), or it is still in
            // its transaction (a write conflict on the unique key — measured: the executor's immediate retries ran
            // out before the other committed and the request ended as a 500). A fresh document's insert conflicts on
            // nothing but a unique key, so either way it is that other row.
            throw new TenantModuleEntitlementConcurrencyException();
        }

        return entity;
    }

    private const int WriteConflictCode = 112;

    private static bool IsConcurrentCreate(MongoException exception) => exception switch
    {
        MongoWriteException write => write.WriteError?.Category == ServerErrorCategory.DuplicateKey
                                     || write.WriteError?.Code == WriteConflictCode,
        MongoCommandException command => command.Code is 11000 or WriteConflictCode,
        _ => false
    };

    /// <summary>
    /// BL-500 — every write of an existing row names the version it was read at. Without one the version condition
    /// used to be left out and the write overwrote whatever another screen had saved in between. The validators refuse
    /// such a request with ENTITLEMENT_ROW_VERSION_REQUIRED before it gets here; this is the floor under them.
    /// </summary>
    private static void RequireRowVersion(byte[]? expectedRowVersion)
    {
        if (expectedRowVersion is not { Length: > 0 })
        {
            throw new ArgumentException("An entitlement row is written only against the version it was read at.", nameof(expectedRowVersion));
        }
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
