using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>
/// MOD-0173 S2 reservations on MongoDB, scoped to the caller's Tenant + LE (JWT). Create and release each run in one
/// multi-document transaction: expire the SKU's due reservations (0173-16) → guarded position change → reservation
/// document → audit record → Pending <c>InventoryReservationChanged</c> outbox event. Two parallel creates on the same
/// SKU conflict on the position document; the retried loser re-evaluates <c>AvailableOnHand − Reserved ≥ q</c> against
/// the committed winner (AC-R3).
/// </summary>
public sealed class InventoryReservationRepository : IInventoryReservationRepository
{
    private readonly IMongoClient _client;
    private readonly IMongoCollection<InventoryReservation> _reservations;
    private readonly ReservationPositionOperations _operations;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public InventoryReservationRepository(IMongoClient client, IMongoDatabase database, ITenantContext tenantContext)
    {
        _client = client;
        _reservations = database.GetCollection<InventoryReservation>(InventoryCollections.Reservations);
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
        var reservation = creation.Reservation;
        var sku = ReservationPositionOperations.KeyOf(reservation);

        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        try
        {
            return await session.WithTransactionAsync(async (s, ct) =>
            {
                await CentralAuditTransaction.EnlistAsync(s, ct: ct); // BL-501: the marked command's central-audit event commits with this transaction or not at all.
                await _operations.ExpireDueAsync(s, sku, reservation.CorrelationId, reservation.CreatedBy, now, ct);

                var position = await _operations.TryReserveAsync(s, sku, reservation.Quantity, now, ct);
                if (position is null)
                {
                    throw new UncoveredQuantityException(await _operations.UnreservedAsync(s, sku, ct), reservation.Quantity);
                }

                reservation.CreatedAvailableAfter = position.AvailableOnHand - position.Reserved;
                await _operations.InsertCreatedAsync(s, reservation, creation.Audit, creation.Outbox, ct);
                return new ReservationCreateResult(ReservationCreateOutcome.Created, reservation);
            },
            new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority),
            cancellationToken);
        }
        catch (UncoveredQuantityException uncovered)
        {
            return new ReservationCreateResult(ReservationCreateOutcome.InsufficientAvailable,
                Available: uncovered.Available, Requested: uncovered.Requested);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey
                                             && ex.WriteError.Message.Contains(InventoryIndexConfiguration.ReservationIdempotencyIndex, StringComparison.Ordinal))
        {
            return new ReservationCreateResult(ReservationCreateOutcome.IdempotencyKeyTaken);
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
        return await session.WithTransactionAsync(async (s, ct) =>
        {
            await CentralAuditTransaction.EnlistAsync(s, ct: ct); // BL-501: the marked command's central-audit event commits with this transaction or not at all.
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
        var match = Scope() & f.Eq(x => x.Status, ReservationStatus.Active)
                    & (f.Eq(x => x.ExpiresAt, null) | f.Gt(x => x.ExpiresAt, (DateTimeOffset?)now));
        if (filter.ItemId.HasValue) match &= f.Eq(x => x.ItemId, filter.ItemId.Value);
        if (filter.SkuId.HasValue) match &= f.Eq(x => x.SkuId, filter.SkuId.Value);
        if (filter.SkuLevel.HasValue) match &= f.Eq(x => x.SkuLevel, filter.SkuLevel.Value);

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
