using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class BrandRepository : RepositoryBase<Brand>, IBrandRepository
{
    /// <summary>WP-MDM-BRAND-ACCEPT-01 FIX1 (item 1) — the append-only brand trail.</summary>
    public const string HistoryCollectionName = "mdm_brand_history";

    private readonly IMongoClient _client;
    private readonly IMongoCollection<BrandHistoryEntry> _history;
    private readonly ExternalReferenceReservations _reservations;
    private readonly BrandProductStoreOptions _storeOptions;
    private readonly ILogger<BrandRepository>? _logger;

    public BrandRepository(
        IMongoDatabase database,
        ITenantContext tenantContext,
        BrandProductStoreOptions? storeOptions = null,
        ILogger<BrandRepository>? logger = null)
        : base(database, tenantContext, "mdm_brands")
    {
        _client = database.Client;
        _history = database.GetCollection<BrandHistoryEntry>(HistoryCollectionName);
        _reservations = new ExternalReferenceReservations(database, TenantId);
        // FIX2 (item 7): no options = the production answer (refuse a write that cannot be atomic).
        _storeOptions = storeOptions ?? new BrandProductStoreOptions();
        _logger = logger;
        IndexOnce.Ensure(database, "mdm_brands", EnsureIndexes);
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
            catch (Exception exception) when (MasterDataAtomicWrite.IsDuplicateKey(exception))
            {
                return BrandWriteOutcome.DuplicateCode;
            }

            if (!await _reservations.TryReserveAsync(session, ExternalReferenceReservation.RecordTypeBrand,
                    brand.Id, brand.BrandCode, brand.ExternalReferences ?? [], cancellationToken))
            {
                return BrandWriteOutcome.ReferenceInUse;
            }

            await AppendAsync(session, Stamp(history, brand.Id, brand.Version), cancellationToken);
            return BrandWriteOutcome.Written;
        }, cancellationToken);
    }

    /// <summary>An edit that changes no external reference (nothing to reserve or release).</summary>
    public Task<BrandWriteOutcome> UpdateEditableFieldsWithHistoryAsync(
        Brand proposed, int expectedVersion, BrandHistoryEntry history, CancellationToken cancellationToken = default)
        => UpdateEditableFieldsWithHistoryAsync(proposed, expectedVersion, history, [], [], cancellationToken);

    public Task<BrandWriteOutcome> UpdateEditableFieldsWithHistoryAsync(
        Brand proposed, int expectedVersion, BrandHistoryEntry history,
        IReadOnlyList<BrandProductExternalReference> added, IReadOnlyList<BrandProductExternalReference> removed,
        CancellationToken cancellationToken = default)
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
        return ConditionalWriteAsync(proposed.Id, proposed.BrandCode, expectedVersion, update, history, added, removed, cancellationToken);
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
        // An archived brand keeps its references reserved: archive is irreversible and the pair stays its own.
        return ConditionalWriteAsync(archived.Id, archived.BrandCode, expectedVersion, update, history, [], [], cancellationToken);
    }

    /// <summary>
    /// FIX2 (M3) — the trail in the order the versions were made. OccurredAtUtc is a clock reading (and a
    /// DateTimeOffset whose offset changes how it sorts in BSON); ResultVersion is the chain itself.
    /// </summary>
    public async Task<IReadOnlyList<BrandHistoryEntry>> GetHistoryAsync(Guid brandId, CancellationToken cancellationToken = default)
        => await _history.Find(
                Builders<BrandHistoryEntry>.Filter.Eq(x => x.TenantId, TenantId)
                & Builders<BrandHistoryEntry>.Filter.Eq(x => x.IsDeleted, false)
                & Builders<BrandHistoryEntry>.Filter.Eq(x => x.BrandId, brandId))
            .SortBy(x => x.ResultVersion)
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
        Guid brandId, string brandCode, int expectedVersion, UpdateDefinition<Brand> update, BrandHistoryEntry history,
        IReadOnlyList<BrandProductExternalReference> added, IReadOnlyList<BrandProductExternalReference> removed,
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

            await _reservations.ReleaseAsync(session, ExternalReferenceReservation.RecordTypeBrand, brandId, removed, cancellationToken);
            if (!await _reservations.TryReserveAsync(session, ExternalReferenceReservation.RecordTypeBrand,
                    brandId, brandCode, added, cancellationToken))
            {
                return BrandWriteOutcome.ReferenceInUse;
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
    /// The brand write, its reservations and its history entry as ONE transaction (FIX2 items 7, 8, 18 — see
    /// <see cref="MasterDataAtomicWrite"/>). A failed history insert (e.g. a duplicate ResultVersion) aborts the whole
    /// transaction, so the brand is left exactly as it was.
    /// </summary>
    private Task<BrandWriteOutcome> AtomicallyAsync(
        Func<IClientSessionHandle?, Task<BrandWriteOutcome>> write, CancellationToken cancellationToken)
        => MasterDataAtomicWrite.RunAsync(_client, _storeOptions, _logger, write, cancellationToken);

    internal static bool IsDuplicateKey(Exception exception) => MasterDataAtomicWrite.IsDuplicateKey(exception);

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
