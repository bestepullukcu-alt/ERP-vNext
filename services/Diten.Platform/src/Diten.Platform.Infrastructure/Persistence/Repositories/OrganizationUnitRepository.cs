using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class OrganizationUnitRepository
    : TenantRepository<OrganizationUnit>, IOrganizationUnitRepository, IOrganizationReportingGraphRepository,
        IOrganizationUnitCodeHistoryRepository
{
    private readonly IPlatformDbContext _dbContext;
    private readonly IMongoCollection<BsonDocument> _structureTokens;
    private readonly IPlatformTransactionExecutor _transactions;

    /// <param name="faultProbe">The commit-boundary probe of the transaction the reporting-line write runs in;
    /// production composition registers the no-op one. Tests use it to hold a writer before its commit.</param>
    public OrganizationUnitRepository(
        IPlatformDbContext dbContext,
        ITenantContext tenantContext,
        IPlatformTransactionFaultProbe? faultProbe = null)
        : base(dbContext.Database, tenantContext, PlatformCollections.OrganizationUnits)
    {
        _dbContext = dbContext;
        // ⚠ PRIMARY, WHATEVER THE CONNECTION STRING SAYS. The guard reads the token and walks the graph outside the
        // transaction; a token from an up-to-date member and a graph from a lagging one would validate against a
        // graph without the edge the token already stands for.
        _structureTokens = dbContext.Database
            .GetCollection<BsonDocument>(PlatformCollections.OrganizationStructureTokens)
            .WithReadPreference(ReadPreference.Primary);

        // ⚠ The REAL executor, built here rather than resolved: a test host that swaps IPlatformTransactionExecutor for a
        // sequential stand-in must not quietly turn this atomic step back into two separate writes.
        _transactions = new PlatformTransactionExecutor(dbContext, faultProbe);
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
            .WithReadPreference(ReadPreference.Primary) // see the constructor: same reason as the token
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
     * ⚠ WHY THE ADVANCE AND THE UNIT WRITE SHARE ONE TRANSACTION. They used to be two writes, on the grounds
     * that a single-document CAS works on a standalone mongod. That left a gap: a second writer could read the
     * ALREADY-ADVANCED token before the first writer's edge was stored, validate against a graph without it,
     * win the next advance and write — both edges landed and the graph was cyclic (MOD-0288, CI "both
     * re-parentings landed"). Inside one transaction a reader sees either the old token (its CAS then loses or
     * conflicts) or the new token together with the edge. The cost: a reporting-line change needs a replica
     * set, and on a standalone mongod it is refused with PlatformTransactionUnavailableException (503) and
     * writes nothing. Creating a unit already needs one (its record code is allocated in a Platform
     * transaction), so the module asks nothing new of its database.
     */

    public async Task<long> ReadStructureTokenAsync(CancellationToken ct = default)
    {
        var doc = await _structureTokens
            .Find(new BsonDocument("_id", TenantContext.TenantId))
            .FirstOrDefaultAsync(ct);

        // Absent means no parent mutation has landed for this tenant yet — token zero, not an error.
        return doc is null ? 0L : doc.GetValue("token", BsonValue.Create(0L)).ToInt64();
    }

    public async Task<bool> TryAdvanceStructureTokenAndUpdateAsync(
        long expectedToken,
        OrganizationUnit unit,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (expectedToken == 0L)
        {
            await EnsureStructureTokenDocumentAsync(ct);
        }

        try
        {
            return await _transactions.ExecuteAsync(async (session, txCt) =>
            {
                var handle = PlatformMongoTransactionSession.Require(session, _dbContext);

                // A lost CAS ends the transaction by THROWING, so the executor aborts it and nothing commits.
                if (!await TryAdvanceStructureTokenAsync(handle, expectedToken, txCt))
                {
                    throw new ReportingLineChangeLostException();
                }

                unit.UpdatedAt = DateTimeOffset.UtcNow;
                var replaced = await Collection.ReplaceOneAsync(handle, UnitFilter(unit.Id), unit, cancellationToken: txCt);
                if (replaced.MatchedCount != 1)
                {
                    throw new ReportingLineChangeLostException(); // deleted/archived under us: the advance rolls back too
                }

                return true;
            }, ct);
        }
        catch (ReportingLineChangeLostException)
        {
            return false;
        }
        catch (PlatformTransactionUnavailableException ex) when (IsWriteConflict(ex.InnerException))
        {
            /*
             * Another writer holds the token inside its own open transaction and kept holding it through every
             * retry. That is the race itself, not an unavailable database: the answer is the loser's 409.
             */
            return false;
        }
        catch (MongoException ex) when (ex.HasErrorLabel("TransientTransactionError"))
        {
            /*
             * The same race met at COMMIT instead of at the write: the executor retries only the body, and a commit
             * refused with this label means the transaction was aborted and nothing was written. Loser's 409, not
             * a raw 500.
             */
            return false;
        }
    }

    private static bool IsWriteConflict(Exception? exception) =>
        exception is MongoCommandException { Code: WriteConflictCode }
        || exception is MongoWriteException { WriteError.Code: WriteConflictCode };

    private const int WriteConflictCode = 112;

    // Not archived as well: both writers here replace a unit read as live, and an archive that landed after that read
    // must not be undone by writing the stale copy back.
    private FilterDefinition<OrganizationUnit> UnitFilter(Guid id) =>
        Builders<OrganizationUnit>.Filter.And(
            ExecutionFilter,
            Builders<OrganizationUnit>.Filter.Eq(x => x.Id, id),
            Builders<OrganizationUnit>.Filter.Eq(x => x.IsArchived, false));

    private sealed class ReportingLineChangeLostException : Exception;

    private async Task<bool> TryAdvanceStructureTokenAsync(
        IClientSessionHandle handle,
        long expectedToken,
        CancellationToken ct)
    {
        var result = await _structureTokens.UpdateOneAsync(
            handle,
            new BsonDocument { { "_id", TenantContext.TenantId }, { "token", expectedToken } },
            new BsonDocument("$inc", new BsonDocument("token", 1L)),
            cancellationToken: ct);

        return result.MatchedCount == 1;
    }

    /*
     * The first mutation for a tenant finds no token document (read as token zero). It is created HERE, outside the
     * transaction, holding zero — what an absent document already meant — so the compare-and-set inside the
     * transaction is always the conditional `$inc`. Measured: creating it inside the transaction made the token
     * COLLECTION's implicit creation part of the transaction too, and the first two such transactions in a
     * database (any tenants) refused each other at commit ("namespace already in use").
     */
    private async Task EnsureStructureTokenDocumentAsync(CancellationToken ct)
    {
        try
        {
            await _structureTokens.UpdateOneAsync(
                new BsonDocument("_id", TenantContext.TenantId),
                new BsonDocument("$setOnInsert", new BsonDocument("token", 0L)),
                new UpdateOptions { IsUpsert = true },
                ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // A concurrent first mutation created it a moment earlier: it exists, which is all this needs.
        }
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

    /// <summary>MOD-0040-FU02 DL-2 — tenant-exact and deliberately WITHOUT the soft-delete filter <see cref="ExistsByCodeAsync"/> has.</summary>
    public Task<bool> ExistsByCodeIncludingDeletedAsync(string code, CancellationToken ct = default) =>
        Collection.Find(Builders<OrganizationUnit>.Filter.And(
                Builders<OrganizationUnit>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
                Builders<OrganizationUnit>.Filter.Eq(x => x.Code, code)))
            .AnyAsync(ct);

    public async Task UpdateAsync(OrganizationUnit organizationUnit, CancellationToken ct = default)
    {
        organizationUnit.UpdatedAt = DateTimeOffset.UtcNow;

        /*
         * ⚠ THE STORED LINES MUST STILL BE THE ONES ON THE ENTITY. This is a whole-document replace from a unit the
         * caller read earlier; without the condition, a rename or an archive that read the unit before a
         * re-parenting landed would write the OLD parent back — a line change that never won the structure token,
         * and with one more re-parenting in between, a cycle. Lines change only through
         * TryAdvanceStructureTokenAndUpdateAsync; here a mismatch writes nothing and is reported.
         */
        var filter = Builders<OrganizationUnit>.Filter.And(
            UnitFilter(organizationUnit.Id),
            Builders<OrganizationUnit>.Filter.Eq(x => x.ParentOrganizationUnitId, organizationUnit.ParentOrganizationUnitId),
            Builders<OrganizationUnit>.Filter.Eq(
                x => x.AdministrativeParentOrganizationUnitId, organizationUnit.AdministrativeParentOrganizationUnitId));
        var result = await Collection.ReplaceOneAsync(filter, organizationUnit, cancellationToken: ct);
        if (result.MatchedCount != 1)
        {
            throw new OrganizationUnitWriteConflictException(organizationUnit.Id);
        }
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
