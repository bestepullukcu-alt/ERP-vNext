using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>
/// The reservation half of a stock transaction (pack ASSUMPTION-0173-12..17), shared by the ledger repository, the
/// reservation repository and the expiry sweeper so the three can never drift apart. Every method takes the session
/// of the caller's Mongo transaction and works inside ONE explicit Tenant + LE scope — there is no member without it.
///
/// <para><b>Position</b> (<c>inventory_sku_positions</c>): <c>AvailableOnHand</c> moves with every AVAILABLE ledger leg,
/// <c>Reserved</c> with every reservation create / release / expiry / consumption. The two guards are conditional
/// filters evaluated by Mongo on the document being written, so check and write are one atomic step and a concurrent
/// writer of the same SKU gets a WriteConflict and re-evaluates after the winner committed.</para>
/// </summary>
internal sealed class ReservationPositionOperations
{
    public const string ActionCreated = "InventoryReservationCreated";
    public const string ActionReleased = "InventoryReservationReleased";
    public const string ActionExpired = "InventoryReservationExpired";
    public const string ActionConsumed = "InventoryReservationConsumed";
    public const string ActionPartiallyConsumed = "InventoryReservationPartiallyConsumed";

    private readonly IMongoCollection<InventorySkuPosition> _positions;
    private readonly IMongoCollection<InventoryReservation> _reservations;
    private readonly IMongoCollection<InventoryReservationAuditRecord> _audit;
    private readonly IMongoCollection<InventoryReservationOutboxMessage> _outbox;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;
    private readonly string? _leSource;

    public ReservationPositionOperations(IMongoDatabase database, Guid tenantId, Guid legalEntityId, string? leSource)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty)
        {
            throw new ArgumentException("Reservation operations need a resolved Tenant + LE scope.");
        }

        _positions = database.GetCollection<InventorySkuPosition>(InventoryCollections.SkuPositions);
        _reservations = database.GetCollection<InventoryReservation>(InventoryCollections.Reservations);
        _audit = database.GetCollection<InventoryReservationAuditRecord>(InventoryCollections.Audit);
        _outbox = database.GetCollection<InventoryReservationOutboxMessage>(InventoryCollections.Outbox);
        _tenantId = tenantId;
        _legalEntityId = legalEntityId;
        _leSource = leSource;
    }

    // ── position ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Moves <c>AvailableOnHand</c> by the net AVAILABLE delta of a posting. A negative delta is applied only if the
    /// SKU keeps <c>AvailableOnHand − Reserved ≥ 0</c> — reserved stock cannot be issued to someone else (0173-14).
    /// </summary>
    public async Task ApplyAvailableDeltaAsync(
        IClientSessionHandle session, SkuKey sku, decimal delta, string baseUomId, DateTimeOffset now, CancellationToken ct)
    {
        if (delta == 0m)
        {
            return;
        }

        var u = Builders<InventorySkuPosition>.Update;
        var move = u.Combine(u.Inc(x => x.AvailableOnHand, delta), u.Inc(x => x.Version, 1), u.Set(x => x.UpdatedAt, now));

        if (delta > 0m)
        {
            await _positions.UpdateOneAsync(
                session,
                PositionKey(sku),
                u.Combine(move,
                    u.SetOnInsert(x => x.Id, Guid.NewGuid()),
                    u.SetOnInsert(x => x.Reserved, 0m),
                    u.SetOnInsert(x => x.BaseUomId, baseUomId),
                    u.SetOnInsert(x => x.CreatedAt, now),
                    u.SetOnInsert(x => x.DeletedAt, null)),
                new UpdateOptions { IsUpsert = true },
                ct);
            return;
        }

        var requested = -delta;
        var updated = await _positions.FindOneAndUpdateAsync(
            session,
            PositionKey(sku) & UnreservedAtLeast(requested),
            move,
            new FindOneAndUpdateOptions<InventorySkuPosition> { ReturnDocument = ReturnDocument.After },
            ct);
        if (updated is null)
        {
            throw new UncoveredQuantityException(await UnreservedAsync(session, sku, ct), requested);
        }
    }

    /// <summary>Holds <paramref name="quantity"/> if <c>AvailableOnHand − Reserved ≥ quantity</c>; null when not covered.</summary>
    public async Task<InventorySkuPosition?> TryReserveAsync(
        IClientSessionHandle session, SkuKey sku, decimal quantity, DateTimeOffset now, CancellationToken ct)
    {
        var u = Builders<InventorySkuPosition>.Update;
        return await _positions.FindOneAndUpdateAsync(
            session,
            PositionKey(sku) & UnreservedAtLeast(quantity),
            u.Combine(u.Inc(x => x.Reserved, quantity), u.Inc(x => x.Version, 1), u.Set(x => x.UpdatedAt, now)),
            new FindOneAndUpdateOptions<InventorySkuPosition> { ReturnDocument = ReturnDocument.After },
            ct);
    }

    public async Task<decimal> UnreservedAsync(IClientSessionHandle session, SkuKey sku, CancellationToken ct)
    {
        var position = await _positions.Find(session, PositionKey(sku)).FirstOrDefaultAsync(ct);
        return position is null ? 0m : position.AvailableOnHand - position.Reserved;
    }

    // ── reservation state changes ───────────────────────────────────────────────────────────────────────────

    public async Task InsertCreatedAsync(
        IClientSessionHandle session, InventoryReservation reservation, InventoryReservationAuditRecord audit,
        InventoryReservationOutboxMessage outbox, CancellationToken ct)
    {
        Stamp(reservation);
        Stamp(audit);
        Stamp(outbox);
        await _reservations.InsertOneAsync(session, reservation, cancellationToken: ct);
        await _audit.InsertOneAsync(session, audit, cancellationToken: ct);
        await _outbox.InsertOneAsync(session, outbox, cancellationToken: ct);
    }

    public async Task<InventoryReservation?> FindAsync(IClientSessionHandle? session, Guid reservationId, CancellationToken ct)
    {
        var filter = ReservationScope() & Builders<InventoryReservation>.Filter.Eq(x => x.Id, reservationId);
        return session is null
            ? await _reservations.Find(filter).FirstOrDefaultAsync(ct)
            : await _reservations.Find(session, filter).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// ACTIVE → RELEASED if the reservation is still ACTIVE at <paramref name="expectedVersion"/> (ETag); returns the
    /// released document, or null on a version / state conflict. The SKU position releases the remaining quantity.
    /// </summary>
    public async Task<InventoryReservation?> TryReleaseAsync(
        IClientSessionHandle session, Guid reservationId, int expectedVersion, Guid correlationId, Guid actor,
        DateTimeOffset now, CancellationToken ct)
    {
        var f = Builders<InventoryReservation>.Filter;
        var u = Builders<InventoryReservation>.Update;
        var before = await _reservations.FindOneAndUpdateAsync(
            session,
            ReservationScope() & f.Eq(x => x.Id, reservationId) & f.Eq(x => x.Status, ReservationStatus.Active)
                & f.Eq(x => x.Version, expectedVersion),
            u.Combine(
                u.Set(x => x.Status, ReservationStatus.Released),
                u.Set(x => x.RemainingQuantity, 0m),
                u.Set(x => x.ClosedAt, now),
                u.Set(x => x.UpdatedAt, now),
                u.Inc(x => x.Version, 1)),
            new FindOneAndUpdateOptions<InventoryReservation> { ReturnDocument = ReturnDocument.Before },
            ct);
        if (before is null)
        {
            return null;
        }

        var sku = KeyOf(before);
        await ReleaseReservedAsync(session, sku, before.RemainingQuantity, now, ct);
        await WriteChangeAsync(session, before, ActionReleased, "Released", before.RemainingQuantity, 0m,
            ReservationStatus.Released, null, correlationId, actor, now, ct);
        before.Status = ReservationStatus.Released;
        before.RemainingQuantity = 0m;
        before.ClosedAt = now;
        before.Version = expectedVersion + 1;
        return before;
    }

    /// <summary>Expires the SKU's ACTIVE reservations with <c>expiresAt ≤ now</c> (0173-16); returns how many.</summary>
    public async Task<int> ExpireDueAsync(
        IClientSessionHandle session, SkuKey sku, Guid correlationId, Guid actor, DateTimeOffset now, CancellationToken ct)
    {
        var f = Builders<InventoryReservation>.Filter;
        var due = await _reservations
            .Find(session, ReservationScope() & SkuFilter(sku) & f.Eq(x => x.Status, ReservationStatus.Active)
                           & f.Ne(x => x.ExpiresAt, null) & f.Lte(x => x.ExpiresAt, now))
            .ToListAsync(ct);

        var u = Builders<InventoryReservation>.Update;
        var expired = 0;
        foreach (var reservation in due)
        {
            var result = await _reservations.UpdateOneAsync(
                session,
                ReservationScope() & f.Eq(x => x.Id, reservation.Id) & f.Eq(x => x.Status, ReservationStatus.Active)
                    & f.Eq(x => x.Version, reservation.Version),
                u.Combine(
                    u.Set(x => x.Status, ReservationStatus.Expired),
                    u.Set(x => x.ClosedAt, now),
                    u.Set(x => x.UpdatedAt, now),
                    u.Inc(x => x.Version, 1)),
                cancellationToken: ct);
            if (result.ModifiedCount == 0)
            {
                continue;
            }

            await ReleaseReservedAsync(session, sku, reservation.RemainingQuantity, now, ct);
            await WriteChangeAsync(session, reservation, ActionExpired, "Expired", reservation.RemainingQuantity,
                reservation.RemainingQuantity, ReservationStatus.Expired, null, correlationId, actor, now, ct);
            expired++;
        }

        return expired;
    }

    /// <summary>
    /// A movement whose (sourceModule, sourceDocumentId, sourceLineId) equals an ACTIVE reservation of the same SKU
    /// consumes it (0173-15), oldest first: the covered quantity is released; partial → still ACTIVE with the remainder,
    /// full → RELEASED. Reservations without a source document are never matched (ASSUMPTION-S2-04). Returns the
    /// quantity consumed.
    /// </summary>
    public async Task<decimal> ConsumeAsync(
        IClientSessionHandle session, SkuKey sku, string sourceModule, string? sourceDocumentId, string? sourceLineId,
        decimal quantity, Guid transactionId, Guid correlationId, Guid actor, DateTimeOffset now, CancellationToken ct)
    {
        if (quantity <= 0m || sourceDocumentId is null)
        {
            return 0m;
        }

        var f = Builders<InventoryReservation>.Filter;
        var matches = await _reservations
            .Find(session, ReservationScope() & SkuFilter(sku) & f.Eq(x => x.Status, ReservationStatus.Active)
                           & f.Eq(x => x.SourceModule, sourceModule) & f.Eq(x => x.SourceDocumentId, sourceDocumentId)
                           & f.Eq(x => x.SourceLineId, sourceLineId))
            .SortBy(x => x.CreatedAt)
            .ToListAsync(ct);

        var u = Builders<InventoryReservation>.Update;
        var left = quantity;
        foreach (var reservation in matches)
        {
            if (left <= 0m)
            {
                break;
            }

            var take = Math.Min(left, reservation.RemainingQuantity);
            var remaining = reservation.RemainingQuantity - take;
            var status = remaining == 0m ? ReservationStatus.Released : ReservationStatus.Active;
            var update = u.Combine(
                u.Set(x => x.RemainingQuantity, remaining),
                u.Set(x => x.Status, status),
                u.Set(x => x.UpdatedAt, now),
                u.Inc(x => x.Version, 1));
            if (status == ReservationStatus.Released)
            {
                update = u.Combine(update, u.Set(x => x.ClosedAt, now));
            }

            var result = await _reservations.UpdateOneAsync(
                session,
                ReservationScope() & f.Eq(x => x.Id, reservation.Id) & f.Eq(x => x.Version, reservation.Version),
                update,
                cancellationToken: ct);
            if (result.ModifiedCount == 0)
            {
                continue;
            }

            await ReleaseReservedAsync(session, sku, take, now, ct);
            var full = status == ReservationStatus.Released;
            await WriteChangeAsync(session, reservation, full ? ActionConsumed : ActionPartiallyConsumed,
                full ? "Consumed" : "PartiallyConsumed", take, remaining, status, transactionId, correlationId, actor, now, ct);
            left -= take;
        }

        return quantity - left;
    }

    public async Task WriteChangeAsync(
        IClientSessionHandle session, InventoryReservation reservation, string action, string change,
        decimal changedQuantity, decimal remaining, ReservationStatus status, Guid? transactionId, Guid correlationId,
        Guid actor, DateTimeOffset now, CancellationToken ct)
    {
        var audit = new InventoryReservationAuditRecord
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Action = action,
            ReservationId = reservation.Id,
            ItemId = reservation.ItemId,
            SkuId = reservation.SkuId,
            SkuLevel = reservation.SkuLevel,
            ChangedQuantity = changedQuantity,
            RemainingQuantity = remaining,
            Status = status,
            ActorUserId = actor,
            CorrelationId = correlationId,
            TransactionId = transactionId,
            LeSource = _leSource,
            OccurredAt = now
        };
        var outbox = new InventoryReservationOutboxMessage
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            AggregateId = reservation.Id,
            CorrelationId = correlationId,
            OccurredAt = now,
            Payload = new InventoryReservationChangedPayload
            {
                ReservationId = reservation.Id,
                Change = change,
                ItemId = reservation.ItemId,
                SkuId = reservation.SkuId,
                SkuLevel = reservation.SkuLevel,
                ChangedQuantity = changedQuantity,
                RemainingQuantity = remaining,
                Status = status,
                SourceModule = reservation.SourceModule,
                SourceDocumentId = reservation.SourceDocumentId,
                SourceLineId = reservation.SourceLineId,
                TransactionId = transactionId
            }
        };
        Stamp(audit);
        Stamp(outbox);
        await _audit.InsertOneAsync(session, audit, cancellationToken: ct);
        await _outbox.InsertOneAsync(session, outbox, cancellationToken: ct);
    }

    public static SkuKey KeyOf(InventoryReservation reservation) => new(reservation.ItemId, reservation.SkuId, reservation.SkuLevel);

    // ── filters ───────────────────────────────────────────────────────────────────────────────────────────

    private Task ReleaseReservedAsync(IClientSessionHandle session, SkuKey sku, decimal quantity, DateTimeOffset now, CancellationToken ct)
    {
        var u = Builders<InventorySkuPosition>.Update;
        return _positions.UpdateOneAsync(
            session,
            PositionKey(sku),
            u.Combine(u.Inc(x => x.Reserved, -quantity), u.Inc(x => x.Version, 1), u.Set(x => x.UpdatedAt, now)),
            cancellationToken: ct);
    }

    private FilterDefinition<InventorySkuPosition> PositionKey(SkuKey sku)
    {
        var f = Builders<InventorySkuPosition>.Filter;
        return f.Eq(x => x.TenantId, (Guid?)_tenantId)
               & f.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)
               & f.Eq(x => x.IsDeleted, false)
               & f.Eq(x => x.ItemId, sku.ItemId)
               & f.Eq(x => x.SkuId, sku.SkuId)
               & f.Eq(x => x.SkuLevel, sku.SkuLevel);
    }

    /// <summary><c>AvailableOnHand − Reserved ≥ q</c>, evaluated by Mongo on the document being updated.</summary>
    private static FilterDefinition<InventorySkuPosition> UnreservedAtLeast(decimal quantity)
        => new BsonDocumentFilterDefinition<InventorySkuPosition>(new BsonDocument("$expr",
            new BsonDocument("$gte", new BsonArray
            {
                new BsonDocument("$subtract", new BsonArray
                {
                    "$" + nameof(InventorySkuPosition.AvailableOnHand),
                    "$" + nameof(InventorySkuPosition.Reserved)
                }),
                new BsonDecimal128(quantity)
            })));

    private FilterDefinition<InventoryReservation> ReservationScope()
        => Builders<InventoryReservation>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId)
           & Builders<InventoryReservation>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)
           & Builders<InventoryReservation>.Filter.Eq(x => x.IsDeleted, false);

    private static FilterDefinition<InventoryReservation> SkuFilter(SkuKey sku)
        => Builders<InventoryReservation>.Filter.Eq(x => x.ItemId, sku.ItemId)
           & Builders<InventoryReservation>.Filter.Eq(x => x.SkuId, sku.SkuId)
           & Builders<InventoryReservation>.Filter.Eq(x => x.SkuLevel, sku.SkuLevel);

    private void Stamp(EntityBase entity)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.IsDeleted = false;
        entity.DeletedAt = null;
    }
}
