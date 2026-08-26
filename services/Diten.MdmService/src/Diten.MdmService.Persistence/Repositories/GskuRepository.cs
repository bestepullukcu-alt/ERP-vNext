using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class GskuRepository : IGskuRepository
{
    private readonly IMongoCollection<Gsku> _gskus;
    private readonly IMongoCollection<CodeReservation> _reservations;
    private readonly Guid _tenantId;

    public GskuRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _gskus = database.GetCollection<Gsku>("mdm_gskus");
        _reservations = database.GetCollection<CodeReservation>("mdm_code_reservations");
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public Task<Gsku?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _gskus.Find(ActiveFilter & Builders<Gsku>.Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(cancellationToken);

    public async Task<Gsku?> GetReferenceableByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _gskus.Find(
                ReferenceableFilter & Builders<Gsku>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Gsku>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _gskus.Find(ActiveFilter & Builders<Gsku>.Filter.In(x => x.Id, ids))
            .ToListAsync(cancellationToken);
    }

    public async Task<GskuPage> GetReferenceablePageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        CancellationToken cancellationToken = default)
    {
        var filter = ReferenceableFilter;
        if (!string.IsNullOrWhiteSpace(canonicalCodeSearch))
        {
            filter &= Builders<Gsku>.Filter.Regex(
                x => x.CanonicalCode,
                new BsonRegularExpression("^" + System.Text.RegularExpressions.Regex.Escape(canonicalCodeSearch)));
        }

        var totalCount = await _gskus.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _gskus.Find(filter)
            .SortBy(x => x.CanonicalCode)
            .ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);
        return new(items, totalCount);
    }

    public async Task<GskuPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        CancellationToken cancellationToken = default)
    {
        var filter = ActiveFilter;
        if (!string.IsNullOrWhiteSpace(canonicalCodeSearch))
        {
            filter &= Builders<Gsku>.Filter.Regex(
                x => x.CanonicalCode,
                new BsonRegularExpression("^" + System.Text.RegularExpressions.Regex.Escape(canonicalCodeSearch)));
        }

        var totalCount = await _gskus.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _gskus.Find(filter)
            .SortBy(x => x.CanonicalCode)
            .ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);
        return new(items, totalCount);
    }

    public async Task<IReadOnlyList<Guid>> FindIdsByCanonicalCodeAsync(
        string canonicalCodeSearch,
        CancellationToken cancellationToken = default)
        => await _gskus.Find(
                ActiveFilter & Builders<Gsku>.Filter.Regex(
                    x => x.CanonicalCode,
                    new BsonRegularExpression("^" + System.Text.RegularExpressions.Regex.Escape(canonicalCodeSearch))))
            .Project(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<Gsku?> GetByCreationCommandIdAsync(string creationCommandId, CancellationToken cancellationToken = default)
        => _gskus.Find(TenantFilter & Builders<Gsku>.Filter.Eq(x => x.CreationCommandId, creationCommandId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<GskuCreateResult> CreateDraftAsync(Gsku gsku, CancellationToken cancellationToken = default)
    {
        var existing = await GetByCreationCommandIdAsync(gsku.CreationCommandId, cancellationToken);
        if (existing is not null)
        {
            return SameFacts(existing, gsku) ? new(true, existing) : new(false, existing, "CREATION_COMMAND_PAIR_CONFLICT");
        }

        var reservationFilter = Builders<CodeReservation>.Filter.Eq(x => x.TenantId, _tenantId)
            & Builders<CodeReservation>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<CodeReservation>.Filter.Eq(x => x.Id, gsku.CodeReservationId)
            & Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.Gsku)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservationState, CodeReservationState.Consumed)
            & Builders<CodeReservation>.Filter.Eq(x => x.ConsumedEntityId, gsku.Id)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservedCode, gsku.CanonicalCode)
            & Builders<CodeReservation>.Filter.In(x => x.BindingState,
                [CodeReservationBindingState.PendingIdentityWrite, CodeReservationBindingState.Confirmed]);
        if (!await _reservations.Find(reservationFilter).AnyAsync(cancellationToken))
        {
            return new(false, null, "CODE_RESERVATION_REQUIRED");
        }

        if (gsku.AuditIntents.Count is 0 or > AuditIntentLimits.MaxPerAggregate
            || gsku.AuditIntents.Any(x => x.TenantId != _tenantId))
        {
            return new(false, null, "AUDIT_INTENT_CONTRACT_INVALID");
        }

        gsku.TenantId = _tenantId;
        gsku.CreatedAt = DateTimeOffset.UtcNow;
        gsku.UpdatedAt = gsku.CreatedAt;
        gsku.IsDeleted = false;
        gsku.Version = 0;
        try
        {
            await _gskus.InsertOneAsync(gsku, cancellationToken: cancellationToken);
            return new(true, gsku);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByCreationCommandIdAsync(gsku.CreationCommandId, cancellationToken);
            return existing is not null && SameFacts(existing, gsku)
                ? new(true, existing)
                : new(false, existing, "CREATION_COMMAND_PAIR_CONFLICT");
        }
    }

    public async Task<GskuUpdateResult> UpdateDraftAsync(
        Gsku gsku,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        if (gsku.AuditIntents.Count == 0)
        {
            return new(false, null, "AUDIT_INTENT_CONTRACT_INVALID");
        }

        var newIntent = gsku.AuditIntents[^1];
        var filter = ActiveFilter
            & Builders<Gsku>.Filter.Eq(x => x.Id, gsku.Id)
            & Builders<Gsku>.Filter.Eq(x => x.Version, expectedVersion)
            & Builders<Gsku>.Filter.Eq(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Draft)
            & Builders<Gsku>.Filter.Where(x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate);
        var update = Builders<Gsku>.Update
            .Set(x => x.PackQuantity, gsku.PackQuantity)
            .Set(x => x.PackUomCode, gsku.PackUomCode)
            .Set(x => x.PackApplicabilitySelection, gsku.PackApplicabilitySelection)
            .Set(x => x.PackUomSelection, gsku.PackUomSelection)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Inc(x => x.Version, 1)
            .Push(x => x.AuditIntents, newIntent);
        var updated = await _gskus.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<Gsku> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return updated is null
            ? new(false, null, "CONCURRENCY_CONFLICT")
            : new(true, updated);
    }

    private static bool SameFacts(Gsku left, Gsku right)
        => left.Id == right.Id
           && left.ProductDefinitionRevisionId == right.ProductDefinitionRevisionId
           && left.CodeReservationId == right.CodeReservationId
           && left.CanonicalCode == right.CanonicalCode
           && left.CreationCommandId == right.CreationCommandId;

    private void EnsureIndexes()
    {
        _gskus.Indexes.CreateMany([
            new CreateIndexModel<Gsku>(Builders<Gsku>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CreationCommandId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_gskus_tenant_command" }),
            new CreateIndexModel<Gsku>(Builders<Gsku>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CodeReservationId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_gskus_tenant_reservation" }),
            new CreateIndexModel<Gsku>(Builders<Gsku>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CanonicalCode),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_gskus_tenant_code" }),
            new CreateIndexModel<Gsku>(Builders<Gsku>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ProductDefinitionRevisionId),
                new CreateIndexOptions { Name = "ix_mdm_gskus_tenant_revision" })
        ]);
    }

    private FilterDefinition<Gsku> TenantFilter => Builders<Gsku>.Filter.Eq(x => x.TenantId, _tenantId);
    private FilterDefinition<Gsku> ActiveFilter => TenantFilter & Builders<Gsku>.Filter.Eq(x => x.IsDeleted, false);
    private FilterDefinition<Gsku> ReferenceableFilter =>
        ActiveFilter & Builders<Gsku>.Filter.In(
            x => x.LifecycleStatus,
            [ProductIdentityLifecycleStatus.Draft, ProductIdentityLifecycleStatus.IdentityApproved]);
}
