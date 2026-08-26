using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class FinishedGoodRepository : IFinishedGoodRepository
{
    private const string CollectionName = "mdm_finished_goods";
    private readonly IMongoCollection<FinishedGood> _finishedGoods;
    private readonly IMongoCollection<Gsku> _gskus;
    private readonly IMongoCollection<CodeReservation> _reservations;
    private readonly Guid _tenantId;

    public FinishedGoodRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _finishedGoods = database.GetCollection<FinishedGood>(CollectionName);
        _gskus = database.GetCollection<Gsku>("mdm_gskus");
        _reservations = database.GetCollection<CodeReservation>("mdm_code_reservations");
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<FinishedGood?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _finishedGoods.Find(ActiveTenantFilter & Builders<FinishedGood>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FinishedGood?> GetByCreationCommandIdAsync(
        string creationCommandId,
        CancellationToken cancellationToken = default)
        => await _finishedGoods.Find(
                TenantIncludingDeletedFilter
                & Builders<FinishedGood>.Filter.Eq(x => x.CreationCommandId, creationCommandId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FinishedGood?> GetByReservationIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
        => await _finishedGoods.Find(
                TenantIncludingDeletedFilter
                & Builders<FinishedGood>.Filter.Eq(x => x.CodeReservationId, reservationId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FinishedGoodPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        IReadOnlyCollection<Guid>? matchingGskuIds,
        CancellationToken cancellationToken = default)
    {
        var filter = ActiveTenantFilter;
        if (!string.IsNullOrWhiteSpace(canonicalCodeSearch))
        {
            var codeFilter = Builders<FinishedGood>.Filter.Regex(
                x => x.CanonicalCode,
                new BsonRegularExpression("^" + System.Text.RegularExpressions.Regex.Escape(canonicalCodeSearch)));
            filter &= matchingGskuIds is { Count: > 0 }
                ? Builders<FinishedGood>.Filter.Or(
                    codeFilter,
                    Builders<FinishedGood>.Filter.In(x => x.GskuId, matchingGskuIds))
                : codeFilter;
        }

        var totalCount = await _finishedGoods.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _finishedGoods.Find(filter)
            .SortBy(x => x.CanonicalCode)
            .ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);
        return new(items, totalCount);
    }

    public async Task<FinishedGoodCreateResult> CreateDraftAsync(
        FinishedGood finishedGood,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetByCreationCommandIdAsync(finishedGood.CreationCommandId, cancellationToken);
        if (existing is not null)
        {
            if (existing.IsDeleted)
            {
                return new(false, existing, "CREATION_COMMAND_TOMBSTONED");
            }

            return SameFacts(existing, finishedGood)
                ? new(true, existing)
                : new(false, existing, "IDEMPOTENCY_KEY_CONFLICT");
        }

        if (finishedGood.Id == Guid.Empty
            || finishedGood.GskuId == Guid.Empty
            || finishedGood.CodeReservationId == Guid.Empty
            || string.IsNullOrWhiteSpace(finishedGood.CanonicalCode)
            || string.IsNullOrWhiteSpace(finishedGood.CreationCommandId)
            || finishedGood.LifecycleStatus != ProductIdentityLifecycleStatus.Draft)
        {
            return new(false, null, "FINISHED_GOOD_CONTRACT_INVALID");
        }

        var referenceableGskuFilter = Builders<Gsku>.Filter.Eq(x => x.TenantId, _tenantId)
            & Builders<Gsku>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<Gsku>.Filter.Eq(x => x.Id, finishedGood.GskuId)
            & Builders<Gsku>.Filter.In(
                x => x.LifecycleStatus,
                [ProductIdentityLifecycleStatus.Draft, ProductIdentityLifecycleStatus.IdentityApproved]);
        if (!await _gskus.Find(referenceableGskuFilter).AnyAsync(cancellationToken))
        {
            return new(false, null, "GSKU_NOT_REFERENCEABLE");
        }

        var reservationFilter = Builders<CodeReservation>.Filter.Eq(x => x.TenantId, _tenantId)
            & Builders<CodeReservation>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<CodeReservation>.Filter.Eq(x => x.Id, finishedGood.CodeReservationId)
            & Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.FinishedGood)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservationState, CodeReservationState.Consumed)
            & Builders<CodeReservation>.Filter.Eq(x => x.ConsumedEntityId, finishedGood.Id)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservedCode, finishedGood.CanonicalCode)
            & Builders<CodeReservation>.Filter.In(
                x => x.BindingState,
                [CodeReservationBindingState.PendingIdentityWrite, CodeReservationBindingState.Confirmed]);
        if (!await _reservations.Find(reservationFilter).AnyAsync(cancellationToken))
        {
            return new(false, null, "CODE_RESERVATION_REQUIRED");
        }

        if (finishedGood.AuditIntents.Count is 0 or > AuditIntentLimits.MaxPerAggregate
            || finishedGood.AuditIntents.Any(intent =>
                intent.TenantId != _tenantId
                || intent.AggregateType != AuditAggregateType.FinishedGood
                || intent.AggregateId != finishedGood.Id))
        {
            return new(false, null, "AUDIT_INTENT_CONTRACT_INVALID");
        }

        finishedGood.TenantId = _tenantId;
        finishedGood.CreatedAt = DateTimeOffset.UtcNow;
        finishedGood.UpdatedAt = finishedGood.CreatedAt;
        finishedGood.IsDeleted = false;
        finishedGood.DeletedAt = null;
        finishedGood.Version = 0;

        try
        {
            await _finishedGoods.InsertOneAsync(finishedGood, cancellationToken: cancellationToken);
            return new(true, finishedGood);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByCreationCommandIdAsync(finishedGood.CreationCommandId, cancellationToken)
                ?? await GetByReservationIdAsync(finishedGood.CodeReservationId, cancellationToken);
            return existing is not null && !existing.IsDeleted && SameFacts(existing, finishedGood)
                ? new(true, existing)
                : new(false, existing, "FINISHED_GOOD_DUPLICATE_CONFLICT");
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

    private static FinishedGoodCreateResult AmbiguousWrite()
        => new(false, null, "FINISHED_GOOD_WRITE_OUTCOME_AMBIGUOUS", WriteOutcomeAmbiguous: true);

    private static bool SameFacts(FinishedGood left, FinishedGood right)
        => left.Id == right.Id
           && left.GskuId == right.GskuId
           && left.CodeReservationId == right.CodeReservationId
           && string.Equals(left.CanonicalCode, right.CanonicalCode, StringComparison.Ordinal)
           && string.Equals(left.CreationCommandId, right.CreationCommandId, StringComparison.Ordinal);

    private void EnsureIndexes()
    {
        _finishedGoods.Indexes.CreateMany([
            new CreateIndexModel<FinishedGood>(
                Builders<FinishedGood>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CanonicalCode),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_finished_goods_tenant_code" }),
            new CreateIndexModel<FinishedGood>(
                Builders<FinishedGood>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CodeReservationId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_finished_goods_tenant_reservation" }),
            new CreateIndexModel<FinishedGood>(
                Builders<FinishedGood>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CreationCommandId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_finished_goods_tenant_command" }),
            new CreateIndexModel<FinishedGood>(
                Builders<FinishedGood>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.GskuId),
                new CreateIndexOptions { Name = "ix_mdm_finished_goods_tenant_gsku" })
        ]);
    }

    private FilterDefinition<FinishedGood> TenantIncludingDeletedFilter =>
        Builders<FinishedGood>.Filter.Eq(x => x.TenantId, _tenantId);

    private FilterDefinition<FinishedGood> ActiveTenantFilter =>
        TenantIncludingDeletedFilter & Builders<FinishedGood>.Filter.Eq(x => x.IsDeleted, false);
}
