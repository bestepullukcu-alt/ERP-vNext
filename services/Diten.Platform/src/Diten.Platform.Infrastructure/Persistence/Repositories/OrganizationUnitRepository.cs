using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class OrganizationUnitRepository
    : TenantRepository<OrganizationUnit>, IOrganizationUnitRepository, IOrganizationReportingGraphRepository
{
    private readonly IMongoCollection<BsonDocument> _structureTokens;

    public OrganizationUnitRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.OrganizationUnits)
    {
        _structureTokens = dbContext.Database
            .GetCollection<BsonDocument>(PlatformCollections.OrganizationStructureTokens);
    }

    public async Task<IReadOnlyList<OrganizationUnit>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return [];
        }

        return await Collection
            .Find(Builders<OrganizationUnit>.Filter.And(
                ExecutionFilter,
                Builders<OrganizationUnit>.Filter.In(x => x.Id, ids)))
            .ToListAsync(ct);
    }

    /*
     * ── THE GRAPH CONCURRENCY GUARD ───────────────────────────────────────────────────────────────────────
     *
     * One document per tenant, `_id` = TenantId, holding a monotonic counter. A parent mutation reads it
     * before it validates and must advance it before it writes; two racing re-parentings read the same value
     * and exactly one advance succeeds.
     *
     * ⚠ WHY A DOCUMENT AND NOT A LOCK. The service runs as more than one process. A `lock` or a semaphore
     * protects one instance while the other writes the other half of the cycle — and a cycle needs exactly
     * two writers, so an in-process guard is precisely the guard that cannot see the case it exists for. This
     * document is the one place every process meets.
     *
     * ⚠ WHY NOT A TRANSACTION. A multi-document condition would express the rule directly, but Mongo offers
     * transactions only on a replica set, and dev runs standalone. A single-document CAS is available
     * everywhere this service is, which is the property that matters for a guard.
     */

    public async Task<long> ReadStructureTokenAsync(CancellationToken ct = default)
    {
        var doc = await _structureTokens
            .Find(new BsonDocument("_id", TenantContext.TenantId))
            .FirstOrDefaultAsync(ct);

        // Absent means no parent mutation has landed for this tenant yet — token zero, not an error.
        return doc is null ? 0L : doc.GetValue("token", BsonValue.Create(0L)).ToInt64();
    }

    public async Task<bool> TryAdvanceStructureTokenAsync(long expectedToken, CancellationToken ct = default)
    {
        if (expectedToken == 0L)
        {
            /*
             * The first mutation for this tenant creates the document. `_id` is unique by definition, so the
             * insert IS the compare-and-set: two writers who both read zero race here and one loses.
             */
            try
            {
                await _structureTokens.InsertOneAsync(
                    new BsonDocument { { "_id", TenantContext.TenantId }, { "token", 1L } },
                    cancellationToken: ct);
                return true;
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        var result = await _structureTokens.UpdateOneAsync(
            new BsonDocument { { "_id", TenantContext.TenantId }, { "token", expectedToken } },
            new BsonDocument("$inc", new BsonDocument("token", 1L)),
            cancellationToken: ct);

        return result.MatchedCount == 1;
    }

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<OrganizationUnit>>
        {
            ExecutionFilter,
            Builders<OrganizationUnit>.Filter.Eq(x => x.Code, code)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<OrganizationUnit>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return Collection.Find(Builders<OrganizationUnit>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task UpdateAsync(OrganizationUnit organizationUnit, CancellationToken ct = default)
    {
        organizationUnit.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<OrganizationUnit>.Filter.And(
            ExecutionFilter,
            Builders<OrganizationUnit>.Filter.Eq(x => x.Id, organizationUnit.Id));
        await Collection.ReplaceOneAsync(filter, organizationUnit, cancellationToken: ct);
    }

    public override async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<OrganizationUnit>.Filter.And(
            ExecutionFilter,
            Builders<OrganizationUnit>.Filter.Eq(x => x.Id, id));
        var now = DateTimeOffset.UtcNow;
        var update = Builders<OrganizationUnit>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, now)
            .Set(x => x.UpdatedAt, now);
        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
