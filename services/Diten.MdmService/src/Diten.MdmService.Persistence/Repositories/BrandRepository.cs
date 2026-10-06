using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class BrandRepository : RepositoryBase<Brand>, IBrandRepository
{
    /// <summary>WP-MDM-BRAND-ACCEPT-01 FIX1 (item 1) — the append-only brand trail.</summary>
    public const string HistoryCollectionName = "mdm_brand_history";

    /// <summary>
    /// How many times one write is run again after MongoDB reports a transient transaction error. Two creates of one code
    /// in open transactions meet as a write conflict until the first commits; then the retry meets the unique index and
    /// answers DuplicateCode. A short, growing pause lets the first one commit.
    /// </summary>
    private const int MaxTransactionAttempts = 10;

    private readonly IMongoClient _client;
    private readonly IMongoCollection<BrandHistoryEntry> _history;

    public BrandRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "mdm_brands")
    {
        _client = database.Client;
        _history = database.GetCollection<BrandHistoryEntry>(HistoryCollectionName);
        EnsureIndexes();
    }

    public Task<BrandWriteOutcome> CreateWithHistoryAsync(
        Brand brand, BrandHistoryEntry history, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        brand.TenantId = TenantId;
        brand.CreatedAt = now;
        brand.UpdatedAt = now;
        brand.IsDeleted = false;
        return AtomicallyAsync(async session =>
        {
            try
            {
                if (session is null)
                    await Collection.InsertOneAsync(brand, cancellationToken: cancellationToken);
                else
                    await Collection.InsertOneAsync(session, brand, cancellationToken: cancellationToken);
            }
            catch (Exception exception) when (IsDuplicateKey(exception))
            {
                return BrandWriteOutcome.DuplicateCode;
            }

            await AppendAsync(session, Stamp(history, brand.Id, brand.Version), cancellationToken);
            return BrandWriteOutcome.Written;
        }, cancellationToken);
    }

    public Task<BrandWriteOutcome> UpdateEditableFieldsWithHistoryAsync(
        Brand proposed, int expectedVersion, BrandHistoryEntry history, CancellationToken cancellationToken = default)
    {
        // Only the editable fields: IsArchived / ArchivedAt / ArchivedBy, the code, the creator and the tenant are never
        // part of an edit, so an edit racing an archive can never bring an archived brand back.
        var update = Builders<Brand>.Update
            .Set(x => x.BrandName, proposed.BrandName)
            .Set(x => x.BrandStatus, proposed.BrandStatus)
            .Set(x => x.Description, proposed.Description)
            .Set(x => x.OwnerCompanyId, proposed.OwnerCompanyId)
            .Set(x => x.BusinessUnitId, proposed.BusinessUnitId)
            .Set(x => x.TherapeuticAreaId, proposed.TherapeuticAreaId)
            .Set(x => x.EffectiveFrom, proposed.EffectiveFrom)
            .Set(x => x.EffectiveTo, proposed.EffectiveTo)
            .Set(x => x.ExternalReferences, proposed.ExternalReferences)
            .Set(x => x.UpdatedBy, proposed.UpdatedBy)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Inc(x => x.Version, 1);
        return ConditionalWriteAsync(proposed.Id, expectedVersion, update, history, cancellationToken);
    }

    public Task<BrandWriteOutcome> ArchiveWithHistoryAsync(
        Brand archived, int expectedVersion, BrandHistoryEntry history, CancellationToken cancellationToken = default)
    {
        var update = Builders<Brand>.Update
            .Set(x => x.IsArchived, true)
            .Set(x => x.BrandStatus, archived.BrandStatus)
            .Set(x => x.ArchivedAt, archived.ArchivedAt)
            .Set(x => x.ArchivedBy, archived.ArchivedBy)
            .Set(x => x.UpdatedBy, archived.UpdatedBy)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Inc(x => x.Version, 1);
        return ConditionalWriteAsync(archived.Id, expectedVersion, update, history, cancellationToken);
    }

    public async Task<IReadOnlyList<BrandHistoryEntry>> GetHistoryAsync(Guid brandId, CancellationToken cancellationToken = default)
        => await _history.Find(
                Builders<BrandHistoryEntry>.Filter.Eq(x => x.TenantId, TenantId)
                & Builders<BrandHistoryEntry>.Filter.Eq(x => x.IsDeleted, false)
                & Builders<BrandHistoryEntry>.Filter.Eq(x => x.BrandId, brandId))
            .SortBy(x => x.OccurredAtUtc)
            .ThenBy(x => x.ResultVersion)
            .ToListAsync(cancellationToken);

    public async Task<Brand?> FindByExternalReferenceAsync(
        string sourceSystem, string externalId, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var filter = TenantFilter & Builders<Brand>.Filter.ElemMatch(x => x.ExternalReferences,
            Builders<BrandProductExternalReference>.Filter.Regex(x => x.SourceSystem, ExactIgnoringCase(sourceSystem))
            & Builders<BrandProductExternalReference>.Filter.Regex(x => x.ExternalId, ExactIgnoringCase(externalId)));
        if (excludeId is { } id)
        {
            filter &= Builders<Brand>.Filter.Ne(x => x.Id, id);
        }

        return await Collection.Find(filter).SortBy(x => x.BrandCode).Limit(1).FirstOrDefaultAsync(cancellationToken);
    }

    private Task<BrandWriteOutcome> ConditionalWriteAsync(
        Guid brandId, int expectedVersion, UpdateDefinition<Brand> update, BrandHistoryEntry history,
        CancellationToken cancellationToken)
    {
        var filter = TenantFilter
                     & Builders<Brand>.Filter.Eq(x => x.Id, brandId)
                     & Builders<Brand>.Filter.Eq(x => x.IsArchived, false)
                     & Builders<Brand>.Filter.Eq(x => x.Version, expectedVersion);
        return AtomicallyAsync(async session =>
        {
            var result = session is null
                ? await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
                : await Collection.UpdateOneAsync(session, filter, update, cancellationToken: cancellationToken);
            if (result.ModifiedCount != 1)
            {
                return BrandWriteOutcome.Stale;
            }

            await AppendAsync(session, Stamp(history, brandId, expectedVersion + 1), cancellationToken);
            return BrandWriteOutcome.Written;
        }, cancellationToken);
    }

    private BrandHistoryEntry Stamp(BrandHistoryEntry history, Guid brandId, int resultVersion)
    {
        history.TenantId = TenantId;
        history.BrandId = brandId;
        history.ResultVersion = resultVersion;
        history.IsDeleted = false;
        history.CreatedAt = history.OccurredAtUtc;
        history.UpdatedAt = history.OccurredAtUtc;
        history.Version = 0;
        return history;
    }

    private Task AppendAsync(IClientSessionHandle? session, BrandHistoryEntry entry, CancellationToken cancellationToken)
        => session is null
            ? _history.InsertOneAsync(entry, cancellationToken: cancellationToken)
            : _history.InsertOneAsync(session, entry, cancellationToken: cancellationToken);

    /// <summary>
    /// Runs one brand write and its history entry as ONE transaction, committed only when the write happened. A server
    /// without transactions (a standalone mongod — the driver refuses before asking) runs the same two writes in order,
    /// brand first: the record is never lost on the replica sets this service runs on, and a standalone dev database keeps
    /// working as it did. A transient transaction error (two writers on the same brand or code) is retried.
    /// </summary>
    private async Task<BrandWriteOutcome> AtomicallyAsync(
        Func<IClientSessionHandle?, Task<BrandWriteOutcome>> write, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
            try
            {
                session.StartTransaction();
            }
            catch (NotSupportedException)
            {
                return await write(null);
            }

            try
            {
                var outcome = await write(session);
                if (outcome == BrandWriteOutcome.Written)
                {
                    await session.CommitTransactionAsync(cancellationToken);
                }
                else
                {
                    await session.AbortTransactionAsync(cancellationToken);
                }

                return outcome;
            }
            catch (MongoException exception) when (
                attempt < MaxTransactionAttempts && exception.HasErrorLabel("TransientTransactionError"))
            {
                await AbortQuietlyAsync(session);
                await Task.Delay(TimeSpan.FromMilliseconds(15 * attempt), cancellationToken);
            }
            catch
            {
                await AbortQuietlyAsync(session);
                throw;
            }
        }
    }

    private static async Task AbortQuietlyAsync(IClientSessionHandle session)
    {
        if (!session.IsInTransaction)
        {
            return;
        }

        try
        {
            await session.AbortTransactionAsync(CancellationToken.None);
        }
        catch (MongoException)
        {
            // The transaction is already gone server-side; nothing was committed.
        }
    }

    internal static bool IsDuplicateKey(Exception exception) => exception switch
    {
        MongoWriteException write => write.WriteError?.Category == ServerErrorCategory.DuplicateKey,
        MongoCommandException command => command.Code == 11000,
        _ => false
    };

    internal static BsonRegularExpression ExactIgnoringCase(string value)
        => new($"^{Regex.Escape(value.Trim())}$", "i");

    public async Task<bool> ExistsByCodeAsync(string brandCode, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        // TenantFilter already scopes to the tenant. Archived rows are intentionally NOT excluded: BrandCode is
        // permanently reserved, so an archived code can never be reused (FU01 §3).
        var filter = Builders<Brand>.Filter.And(
            TenantFilter,
            Builders<Brand>.Filter.Eq(x => x.BrandCode, brandCode));

        if (excludeId.HasValue)
        {
            filter &= Builders<Brand>.Filter.Ne(x => x.Id, excludeId.Value);
        }

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Brand>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Collection.Find(TenantFilter)
            .SortBy(x => x.BrandName)
            .ToListAsync(cancellationToken);

    private void EnsureIndexes()
    {
        // Plain unique index — NO partial filter. A partial filter would have to express "not archived", and
        // MongoDB rejects `$ne`/`$not` inside partialFilterExpression, which crash-loops the service at startup.
        // Permanently reserving the code sidesteps the whole problem.
        var codeIndex = new CreateIndexModel<Brand>(
            Builders<Brand>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.BrandCode),
            new CreateIndexOptions { Unique = true, Name = "ux_mdm_brands_tenant_code" });

        // Listing/filtering support. EffectiveFrom and EffectiveTo are DateTimeOffset (stored as BSON arrays)
        // and are deliberately NOT indexed together — two parallel arrays in one index/sort raise
        // "cannot sort with keys that are parallel arrays".
        var listIndex = new CreateIndexModel<Brand>(
            Builders<Brand>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.IsArchived)
                .Ascending(x => x.BrandStatus),
            new CreateIndexOptions { Name = "ix_mdm_brands_tenant_archived_status" });

        Collection.Indexes.CreateMany([codeIndex, listIndex]);

        _history.Indexes.CreateOne(new CreateIndexModel<BrandHistoryEntry>(
            Builders<BrandHistoryEntry>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.BrandId)
                .Ascending(x => x.ResultVersion),
            new CreateIndexOptions { Unique = true, Name = "ux_mdm_brand_history_tenant_brand_version" }));
    }
}
