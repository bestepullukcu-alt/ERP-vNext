using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>
/// MOD-0173 S2 reservations on MongoDB, scoped to the caller's Tenant + LE (JWT). Create and release each run in one
/// multi-document transaction: expire the SKU's due reservations (0173-16) → guarded position change → reservation
/// document → audit record → Pending <c>InventoryReservationChanged</c> outbox event. Two parallel creates on the same
/// SKU conflict on the position document; the retried loser re-evaluates <c>AvailableOnHand − Reserved ≥ q</c> against
/// the committed winner (AC-R3).
/// <para>MOD-0173-FU03 DL-1: the guard runs on every level of the reservation's scope (lot, warehouse, SKU — §8.2); a batch of lines is
/// one transaction (§8.7); every re-run of a transaction body is counted as <see cref="TransactionRetryCounter.Reservation"/>. Reads of
/// reserved quantities use <see cref="ReservationActiveFilter"/> (a due reservation is never counted). A refusal row is written after
/// the refused request, outside any transaction (§21).</para>
/// </summary>
public sealed class InventoryReservationRepository : IInventoryReservationRepository
{
    private readonly IMongoClient _client;
    private readonly IMongoCollection<InventoryReservation> _reservations;
    private readonly IMongoCollection<InventoryReservationPosition> _scopePositions;
    private readonly IMongoCollection<InventoryReservationAuditRecord> _audit;
    private readonly ReservationPositionOperations _operations;
    private readonly TransactionRetryCounter _retries;
    private readonly ILogger<InventoryReservationRepository> _logger;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public InventoryReservationRepository(
        IMongoClient client,
        IMongoDatabase database,
        ITenantContext tenantContext,
        TransactionRetryCounter retries,
        ILogger<InventoryReservationRepository> logger)
    {
        _client = client;
        _reservations = database.GetCollection<InventoryReservation>(InventoryCollections.Reservations);
        _scopePositions = database.GetCollection<InventoryReservationPosition>(InventoryCollections.ReservationPositions);
        _audit = database.GetCollection<InventoryReservationAuditRecord>(InventoryCollections.Audit);
        _retries = retries;
        _logger = logger;
        _tenantId = tenantContext.TenantId;
        _legalEntityId = tenantContext.LegalEntityId;
        _operations = new ReservationPositionOperations(database, _tenantId, _legalEntityId, tenantContext.LegalEntitySource);
    }

    public async Task<InventoryReservation?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => await _reservations
            .Find(Scope() & Builders<InventoryReservation>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ReservationCreateResult> CreateAsync(ReservationCreation creation, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var batch = await CreateBatchAsync([creation], now, cancellationToken);
        return batch.Outcome switch
        {
            ReservationCreateOutcome.Created => new ReservationCreateResult(ReservationCreateOutcome.Created, batch.Reservations[0]),
            ReservationCreateOutcome.InsufficientAvailable => new ReservationCreateResult(ReservationCreateOutcome.InsufficientAvailable,
                Available: batch.Available, Requested: batch.Requested, Level: batch.Level),
            _ => new ReservationCreateResult(batch.Outcome)
        };
    }

    public async Task<ReservationBatchResult> CreateBatchAsync(
        IReadOnlyList<ReservationCreation> creations, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        var attempt = 0;
        var index = 0;
        try
        {
            return await session.WithTransactionAsync(async (s, ct) =>
            {
                await CentralAuditTransaction.EnlistAsync(s, ct: ct); // BL-501: the marked command's central-audit event commits with this transaction or not at all.
                if (++attempt > 1)
                {
                    _retries.Retried(TransactionRetryCounter.Reservation);
                    _logger.LogInformation("Reservation transaction re-run after a transient conflict (attempt {Attempt}).", attempt);
                }

                foreach (var sku in creations.Select(c => ReservationPositionOperations.KeyOf(c.Reservation)).Distinct())
                {
                    var first = creations.First(c => ReservationPositionOperations.KeyOf(c.Reservation) == sku).Reservation;
                    await _operations.ExpireDueAsync(s, sku, first.CorrelationId, first.CreatedBy, now, ct);
                }

                var created = new List<InventoryReservation>(creations.Count);
                for (index = 0; index < creations.Count; index++)
                {
                    var creation = creations[index];
                    var position = await _operations.ReserveAsync(s, creation.Reservation, now, ct);
                    creation.Reservation.CreatedAvailableAfter = position.AvailableOnHand - position.Reserved;
                    await _operations.InsertCreatedAsync(s, creation.Reservation, creation.Audit, creation.Outbox, ct);
                    created.Add(creation.Reservation);
                }

                return new ReservationBatchResult(ReservationCreateOutcome.Created, created);
            },
            new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority),
            cancellationToken);
        }
        catch (UncoveredQuantityException uncovered)
        {
            return new ReservationBatchResult(ReservationCreateOutcome.InsufficientAvailable, [], index,
                uncovered.Available, uncovered.Requested, uncovered.Level);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey
                                             && ex.WriteError.Message.Contains(InventoryIndexConfiguration.ReservationIdempotencyIndex, StringComparison.Ordinal))
        {
            return new ReservationBatchResult(ReservationCreateOutcome.IdempotencyKeyTaken, []);
        }
    }

    public async Task<ReservationReleaseResult> ReleaseAsync(
        Guid reservationId,
        int expectedVersion,
        Guid correlationId,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        var attempt = 0;
        return await session.WithTransactionAsync(async (s, ct) =>
        {
            await CentralAuditTransaction.EnlistAsync(s, ct: ct); // BL-501: the marked command's central-audit event commits with this transaction or not at all.
            if (++attempt > 1)
            {
                _retries.Retried(TransactionRetryCounter.Reservation);
                _logger.LogInformation("Reservation release transaction re-run after a transient conflict (attempt {Attempt}).", attempt);
            }

            var current = await _operations.FindAsync(s, reservationId, ct);
            if (current is null)
            {
                return new ReservationReleaseResult(ReservationReleaseOutcome.NotFound);
            }

            var sku = ReservationPositionOperations.KeyOf(current);
            await _operations.ExpireDueAsync(s, sku, correlationId, actorUserId, now, ct);

            var released = await _operations.TryReleaseAsync(s, reservationId, expectedVersion, correlationId, actorUserId, now, ct);
            if (released is null)
            {
                return new ReservationReleaseResult(ReservationReleaseOutcome.Conflict, current);
            }

            return new ReservationReleaseResult(ReservationReleaseOutcome.Released, released, await _operations.UnreservedAsync(s, sku, ct));
        },
        new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority),
        cancellationToken);
    }

    public async Task<decimal> SumActiveReservedAsync(AvailabilityFilter filter, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var f = Builders<InventoryReservation>.Filter;
        var match = Scope() & ReservationActiveFilter.At(now);
        if (filter.ItemId.HasValue) match &= f.Eq(x => x.ItemId, filter.ItemId.Value);
        if (filter.SkuId.HasValue) match &= f.Eq(x => x.SkuId, filter.SkuId.Value);
        if (filter.SkuLevel.HasValue) match &= f.Eq(x => x.SkuLevel, filter.SkuLevel.Value);
        return await SumRemainingAsync(match, cancellationToken);
    }

    public async Task<InventoryReservation?> FindByIdAsync(Guid reservationId, CancellationToken cancellationToken = default)
        => await _operations.FindAsync(null, reservationId, cancellationToken);

    public async Task<IReadOnlyList<InventoryReservation>> FindByIdempotencyKeysAsync(
        IReadOnlyCollection<string> idempotencyKeys, CancellationToken cancellationToken = default)
        => await _reservations
            .Find(Scope() & Builders<InventoryReservation>.Filter.In(x => x.IdempotencyKey, idempotencyKeys))
            .ToListAsync(cancellationToken);

    public async Task<decimal> SumActiveScopeReservedAsync(
        SkuKey sku, string warehouseId, Guid? lotId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var f = Builders<InventoryReservation>.Filter;
        var match = Scope() & ReservationActiveFilter.At(now)
                    & f.Eq(x => x.ItemId, sku.ItemId) & f.Eq(x => x.SkuId, sku.SkuId) & f.Eq(x => x.SkuLevel, sku.SkuLevel)
                    & f.Eq(x => x.WarehouseId, warehouseId);
        if (lotId.HasValue) match &= f.Eq(x => x.LotId, lotId);
        return await SumRemainingAsync(match, cancellationToken);
    }

    public async Task<decimal> ScopeAvailableOnHandAsync(SkuKey sku, string warehouseId, Guid? lotId, CancellationToken cancellationToken = default)
    {
        var f = Builders<InventoryReservationPosition>.Filter;
        var position = await _scopePositions
            .Find(f.Eq(x => x.TenantId, (Guid?)_tenantId) & f.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId) & f.Eq(x => x.IsDeleted, false)
                  & f.Eq(x => x.ItemId, sku.ItemId) & f.Eq(x => x.SkuId, sku.SkuId) & f.Eq(x => x.SkuLevel, sku.SkuLevel)
                  & f.Eq(x => x.Level, lotId.HasValue ? ReservationScopeLevel.Lot : ReservationScopeLevel.Warehouse)
                  & f.Eq(x => x.WarehouseId, warehouseId) & f.Eq(x => x.LotId, lotId ?? Guid.Empty))
            .FirstOrDefaultAsync(cancellationToken);
        return position?.AvailableOnHand ?? 0m;
    }

    public async Task WriteRefusalAsync(InventoryReservationAuditRecord refusal, CancellationToken cancellationToken = default)
    {
        refusal.TenantId = _tenantId;
        refusal.LegalEntityId = _legalEntityId;
        refusal.IsDeleted = false;
        refusal.DeletedAt = null;
        try
        {
            await _audit.InsertOneAsync(refusal, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            RefusalAuditMetrics.Failed.Add(1, new KeyValuePair<string, object?>("action", refusal.Action));
            _logger.LogWarning(ex, "The refusal audit row {Action} ({ErrorCode}) could not be written; the refusal answer stands.",
                refusal.Action, refusal.ErrorCode);
        }
    }

    private async Task<decimal> SumRemainingAsync(FilterDefinition<InventoryReservation> match, CancellationToken cancellationToken)
    {
        var totals = await _reservations.Aggregate()
            .Match(match)
            .Group(new BsonDocument
            {
                { "_id", BsonNull.Value },
                { "reserved", new BsonDocument("$sum", "$" + nameof(InventoryReservation.RemainingQuantity)) }
            })
            .FirstOrDefaultAsync(cancellationToken);
        return totals is null ? 0m : totals["reserved"].ToDecimal();
    }

    /// <summary>Tenant + LegalEntity + IsDeleted = false — the mandatory entry filter of every reservation query.</summary>
    private FilterDefinition<InventoryReservation> Scope()
        => Builders<InventoryReservation>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId)
           & Builders<InventoryReservation>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)
           & Builders<InventoryReservation>.Filter.Eq(x => x.IsDeleted, false);
}
