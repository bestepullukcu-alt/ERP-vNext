using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class LskuRepository : ILskuRepository
{
    private const string CollectionName = "mdm_lskus";
    private readonly IMongoCollection<Lsku> _lskus;
    private readonly IMongoCollection<Gsku> _gskus;
    private readonly IMongoCollection<CodeReservation> _reservations;
    private readonly Guid _tenantId;

    public LskuRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _lskus = database.GetCollection<Lsku>(CollectionName);
        _gskus = database.GetCollection<Gsku>("mdm_gskus");
        _reservations = database.GetCollection<CodeReservation>("mdm_code_reservations");
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<Lsku?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        (Lsku?)await _lskus.Find(ActiveFilter & Builders<Lsku>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<LskuPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var filter = ActiveFilter;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var escaped = System.Text.RegularExpressions.Regex.Escape(search);
            filter &= Builders<Lsku>.Filter.Or(
                Builders<Lsku>.Filter.Regex(
                    x => x.CanonicalCode,
                    new BsonRegularExpression("^" + escaped)),
                Builders<Lsku>.Filter.Regex(
                    x => x.MarketCode,
                    new BsonRegularExpression("^" + escaped)));
        }

        var totalCount = await _lskus.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _lskus.Find(filter)
            .SortBy(x => x.CanonicalCode)
            .ThenBy(x => x.MarketCode)
            .ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);
        return new(items, totalCount);
    }

    public async Task<Lsku?> GetByCreationCommandIdAsync(
        string creationCommandId,
        CancellationToken cancellationToken = default) =>
        await _lskus.Find(
                TenantIncludingDeletedFilter
                & Builders<Lsku>.Filter.Eq(x => x.CreationCommandId, creationCommandId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Lsku?> GetByReservationIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default) =>
        await _lskus.Find(
                TenantIncludingDeletedFilter
                & Builders<Lsku>.Filter.Eq(x => x.CodeReservationId, reservationId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Lsku?> GetByIdentityKeyAsync(
        Guid gskuId,
        string marketCode,
        CancellationToken cancellationToken = default) =>
        await _lskus.Find(
                TenantIncludingDeletedFilter
                & Builders<Lsku>.Filter.Eq(x => x.GskuId, gskuId)
                & Builders<Lsku>.Filter.Eq(x => x.MarketCode, marketCode))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<LskuCreateResult> CreateDraftAsync(
        Lsku lsku,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetByCreationCommandIdAsync(lsku.CreationCommandId, cancellationToken);
        if (existing is not null)
        {
            return ExistingResult(existing, lsku);
        }

        if (!IsValidContract(lsku))
        {
            return new(false, null, "LSKU_CONTRACT_INVALID");
        }

        var referenceableGskuFilter = Builders<Gsku>.Filter.Eq(x => x.TenantId, _tenantId)
            & Builders<Gsku>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<Gsku>.Filter.Eq(x => x.Id, lsku.GskuId)
            & Builders<Gsku>.Filter.In(
                x => x.LifecycleStatus,
                [ProductIdentityLifecycleStatus.Draft, ProductIdentityLifecycleStatus.IdentityApproved]);
        if (!await _gskus.Find(referenceableGskuFilter).AnyAsync(cancellationToken))
        {
            return new(false, null, "GSKU_NOT_REFERENCEABLE");
        }

        var reservationFilter = Builders<CodeReservation>.Filter.Eq(x => x.TenantId, _tenantId)
            & Builders<CodeReservation>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<CodeReservation>.Filter.Eq(x => x.Id, lsku.CodeReservationId)
            & Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.Lsku)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservationState, CodeReservationState.Consumed)
            & Builders<CodeReservation>.Filter.Eq(x => x.ConsumedEntityId, lsku.Id)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservedCode, lsku.CanonicalCode)
            & Builders<CodeReservation>.Filter.In(
                x => x.BindingState,
                [CodeReservationBindingState.PendingIdentityWrite, CodeReservationBindingState.Confirmed]);
        if (!await _reservations.Find(reservationFilter).AnyAsync(cancellationToken))
        {
            return new(false, null, "CODE_RESERVATION_REQUIRED");
        }

        if (lsku.AuditIntents.Count is 0 or > AuditIntentLimits.MaxPerAggregate
            || lsku.AuditIntents.Any(intent =>
                intent.TenantId != _tenantId
                || intent.AggregateType != AuditAggregateType.Lsku
                || intent.AggregateId != lsku.Id))
        {
            return new(false, null, "AUDIT_INTENT_CONTRACT_INVALID");
        }

        lsku.TenantId = _tenantId;
        lsku.CreatedAt = DateTimeOffset.UtcNow;
        lsku.UpdatedAt = lsku.CreatedAt;
        lsku.IsDeleted = false;
        lsku.DeletedAt = null;
        lsku.Version = 0;

        try
        {
            await _lskus.InsertOneAsync(lsku, cancellationToken: cancellationToken);
            return new(true, lsku);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByCreationCommandIdAsync(lsku.CreationCommandId, cancellationToken);
            if (existing is not null)
            {
                return ExistingResult(existing, lsku);
            }

            existing = await GetByReservationIdAsync(lsku.CodeReservationId, cancellationToken);
            if (existing is not null)
            {
                return ExistingResult(existing, lsku);
            }

            existing = await GetByIdentityKeyAsync(lsku.GskuId, lsku.MarketCode, cancellationToken);
            if (existing is not null)
            {
                return existing.IsDeleted
                    ? new(false, existing, "LSKU_IDENTITY_TOMBSTONED")
                    : new(
                        false,
                        existing,
                        "LSKU_IDENTITY_KEY_CONFLICT",
                        ConflictKind: LskuCreateConflictKind.IdentityKey);
            }

            return new(false, null, "LSKU_DUPLICATE_CONFLICT");
        }
        catch (MongoConnectionException)
        {
            return AmbiguousWrite();
        }
        catch (MongoExecutionTimeoutException)
        {
            return AmbiguousWrite();
        }
        catch (MongoWriteConcernException)
        {
            return AmbiguousWrite();
        }
    }

    private static LskuCreateResult ExistingResult(Lsku existing, Lsku requested)
    {
        if (existing.IsDeleted)
        {
            return new(false, existing, "LSKU_IDENTITY_TOMBSTONED");
        }

        return SameFacts(existing, requested)
            ? new(true, existing)
            : new(
                false,
                existing,
                "LSKU_DUPLICATE_CONFLICT",
                ConflictKind: LskuCreateConflictKind.CommandOrPayload);
    }

    private static bool IsValidContract(Lsku lsku) =>
        lsku.Id != Guid.Empty
        && lsku.GskuId != Guid.Empty
        && lsku.CodeReservationId != Guid.Empty
        && !string.IsNullOrWhiteSpace(lsku.CanonicalCode)
        && !string.IsNullOrWhiteSpace(lsku.CreationCommandId)
        && IsExactIsoAlpha2(lsku.MarketCode)
        && lsku.LifecycleStatus == ProductIdentityLifecycleStatus.Draft
        && string.Equals(lsku.MarketSelection.SetCode, "market", StringComparison.Ordinal)
        && string.Equals(lsku.MarketSelection.ValueCode, lsku.MarketCode, StringComparison.Ordinal)
        && lsku.MarketSelection.CatalogVersionId != Guid.Empty
        && lsku.MarketSelection.CatalogVersionNumber > 0
        && lsku.MarketSelection.ResolutionMode == ReferenceCatalogResolutionMode.Latest
        && lsku.MarketSelection.ResolvedAtUtc != default;

    private static bool IsExactIsoAlpha2(string? value) =>
        value is { Length: 2 }
        && value[0] is >= 'A' and <= 'Z'
        && value[1] is >= 'A' and <= 'Z';

    private static LskuCreateResult AmbiguousWrite() =>
        new(false, null, "LSKU_WRITE_OUTCOME_AMBIGUOUS", WriteOutcomeAmbiguous: true);

    private static bool SameFacts(Lsku left, Lsku right) =>
        left.Id == right.Id
        && left.GskuId == right.GskuId
        && left.CodeReservationId == right.CodeReservationId
        && string.Equals(left.CanonicalCode, right.CanonicalCode, StringComparison.Ordinal)
        && string.Equals(left.CreationCommandId, right.CreationCommandId, StringComparison.Ordinal)
        && string.Equals(left.MarketCode, right.MarketCode, StringComparison.Ordinal)
        && SameSelection(left.MarketSelection, right.MarketSelection);

    private static bool SameSelection(ReferenceCatalogSelection left, ReferenceCatalogSelection right) =>
        string.Equals(left.SetCode, right.SetCode, StringComparison.Ordinal)
        && string.Equals(left.ValueCode, right.ValueCode, StringComparison.Ordinal)
        && left.CatalogVersionId == right.CatalogVersionId
        && left.CatalogVersionNumber == right.CatalogVersionNumber
        && left.ResolutionMode == right.ResolutionMode
        && left.ResolvedAtUtc == right.ResolvedAtUtc;

    private void EnsureIndexes()
    {
        _lskus.Indexes.CreateMany([
            new CreateIndexModel<Lsku>(
                Builders<Lsku>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CanonicalCode),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_lskus_tenant_code" }),
            new CreateIndexModel<Lsku>(
                Builders<Lsku>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CodeReservationId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_lskus_tenant_reservation" }),
            new CreateIndexModel<Lsku>(
                Builders<Lsku>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CreationCommandId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_lskus_tenant_command" }),
            new CreateIndexModel<Lsku>(
                Builders<Lsku>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.GskuId)
                    .Ascending(x => x.MarketCode),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_lskus_tenant_gsku_market" })
        ]);
    }

    private FilterDefinition<Lsku> TenantIncludingDeletedFilter =>
        Builders<Lsku>.Filter.Eq(x => x.TenantId, _tenantId);

    private FilterDefinition<Lsku> ActiveFilter =>
        TenantIncludingDeletedFilter & Builders<Lsku>.Filter.Eq(x => x.IsDeleted, false);
}
