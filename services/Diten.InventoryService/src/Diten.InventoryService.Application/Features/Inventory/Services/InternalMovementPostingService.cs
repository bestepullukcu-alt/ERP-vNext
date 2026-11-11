using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Rules;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>
/// The ledger's posting path, extracted from <c>PostMovementHandler</c> (MOD-0175 pack §5 Q1, ASSUMPTION-0175-03) so the
/// MOD-0175 apply engine posts with the SAME parser, idempotency, master-data / lot validators and
/// <see cref="IInventoryLedgerRepository.PostAsync"/> — no validator is bypassed. The caller names the legal entity and the
/// actor explicitly; the legal entity must be the resolved scope (the repository is bound to it), else the call is refused
/// fail-closed. After a NEW posting (not a replay, not a refusal) every <see cref="IPostedMovementObserver"/> is told,
/// post-commit; an observer failure is logged and never changes the answer.
/// <para>postMovement. Order matters and is the whole design:</para>
/// <list type="number">
/// <item>Idempotency first — a key already committed in this Tenant + LE answers from the durable record: same
/// fingerprint → 200 replay (no upstream call, no write); different fingerprint → 400 IDEMPOTENCY_KEY_REUSED.</item>
/// <item>REVERSAL copies the original (ASSUMPTION-0173-05); any other type is checked against PRODUCT-MASTER and
/// LOCATION — unreachable → 503, nothing written.</item>
/// <item>One atomic posting (transaction + balance legs + audit + Pending outbox). A concurrent winner on the same
/// key or the same original is resolved from the committed record, never by writing twice.</item>
/// </list>
/// </summary>
public sealed class InternalMovementPostingService
{
    private const string PostedAction = "InventoryMovementPosted";
    private const string ReversedAction = "InventoryMovementReversed";

    private readonly IInventoryLedgerRepository _ledger;
    private readonly MovementMasterDataValidator _masterData;
    private readonly ITenantContext _tenantContext;
    private readonly ICorrelationContext _correlation;
    private readonly ILotMasterClient _lots;
    private readonly IEnumerable<IPostedMovementObserver> _observers;
    private readonly ILogger<InternalMovementPostingService> _logger;
    private readonly IExpiryPostingPolicy _expiry;
    private readonly IPostingActorName? _actorName;
    private readonly IInventoryReservationRepository? _reservations;

    public InternalMovementPostingService(
        IInventoryLedgerRepository ledger,
        ILotMasterClient lots,
        MovementMasterDataValidator masterData,
        ITenantContext tenantContext,
        ICorrelationContext correlation,
        IEnumerable<IPostedMovementObserver> observers,
        ILogger<InternalMovementPostingService> logger,
        IExpiryPostingPolicy? expiry = null,
        IPostingActorName? actorName = null,
        IInventoryReservationRepository? reservations = null)
    {
        _actorName = actorName; // WP-MVP1-FU-CLEAN2: DI always passes it; a hand-built service stores no name
        _reservations = reservations; // MOD-0173-FU03 AC-40: DI always passes it; a hand-built service writes no refusal row
        _expiry = expiry ?? new AllowAllExpiryPostingPolicy(); // MOD-0176 F2: DI always passes the registered policy
        _ledger = ledger;
        _lots = lots;
        _masterData = masterData;
        _tenantContext = tenantContext;
        _correlation = correlation;
        _observers = observers;
        _logger = logger;
    }

    /// <summary>
    /// WP-MVP1-FU-CLEAN2 (FU-0173-MOV-ACTORNAME): the name stored with the movement — only when the actor IS the resolved scope user (the
    /// token's, or the user a background flow acts for with its stored name); another actor id never borrows the scope user's name.
    /// </summary>
    private string? NameOf(Guid actorUserId)
        => actorUserId != Guid.Empty && actorUserId == _tenantContext.UserId ? _actorName?.Current : null;

    public async Task<Response<MovementResponseDto>> PostAsync(PostMovementCommand request, Guid legalEntityId, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (legalEntityId != _tenantContext.LegalEntityId)
        {
            throw new InvalidOperationException("An internal posting must name the resolved legal entity; the ledger repository is bound to it.");
        }


        var (movement, errors) = MovementRequestParser.Parse(request);
        if (movement is null)
        {
            return Response<MovementResponseDto>.Fail(errors[0].Code, errors.Select(e => e.Message).ToList(), 400);
        }

        var fingerprint = RequestFingerprint.Compute(movement);

        var committed = await _ledger.FindByIdempotencyKeyAsync(movement.IdempotencyKey, cancellationToken);
        if (committed is not null)
        {
            return Replay(committed, fingerprint);
        }

        // MOD-0173-FU03 §8.3 step 3 (out of the transaction, only when reservationId was sent): a type / direction that neither consumes
        // nor moves reserved stock (REVERSAL included) → 422 RESERVATION_NOT_APPLICABLE; a text that is no UUID → 422 RESERVATION_NOT_FOUND,
        // the same answer as an unknown id. Both leave their refusal row.
        Guid? reservationId = null;
        if (movement.ReservationId is { } reservationText)
        {
            if (!NamesReservation(movement))
            {
                return await RefuseReservationAsync(Facts(movement, reservationText, actorUserId), InventoryErrorCodes.ReservationNotApplicable,
                    $"{WireNames.ToWire(movement.MovementType)} neither consumes nor moves reserved stock: only GOODS_ISSUE_SALES, GOODS_ISSUE_PROD and LOCATION_TRANSFER out of AVAILABLE name a reservation.",
                    new Dictionary<string, object?> { ["movementType"] = WireNames.ToWire(movement.MovementType) }, null, cancellationToken);
            }

            if (!WireParsing.TryParseUuid(reservationText, out var parsedId))
            {
                return await RefuseReservationAsync(Facts(movement, reservationText, actorUserId), InventoryErrorCodes.ReservationNotFound,
                    InventoryErrorCodes.ReservationNotFoundMessage, null, null, cancellationToken);
            }

            reservationId = parsedId;
        }

        InventoryTransaction transaction;
        LotPostingContext? lotContext;
        ReservationRestore? restore = null;
        if (movement.MovementType == MovementType.Reversal)
        {
            var (reversal, refusal, original) = await BuildReversalAsync(movement, fingerprint, cancellationToken);
            if (refusal is not null)
            {
                return refusal;
            }

            transaction = reversal!;
            // MOD-0173-FU03 §8.3.1 (CT-08, r3 boundary): only an explicit restoreReservation: true, only for an original that consumed a
            // reservation it named; otherwise the flag has no effect (a compensation may send the same body for every line).
            if (movement.RestoreReservation && original!.ReservationId is { } consumed && original.ReservationConsumption is { ConsumedQuantity: > 0m } snapshot)
            {
                restore = new ReservationRestore(consumed, snapshot.ConsumedQuantity, NameOf(actorUserId));
            }

            transaction.CreatedBy = actorUserId;
            transaction.CreatedByName = NameOf(actorUserId);
            // WP-0174-P2: the reversal re-runs the serial lifecycle of its original on the serials the registry knows; the
            // lot rules of CT-D4 let a REVERSAL through a Blocked / Expired lot (re-checked in the posting transaction).
            lotContext = LotPostingContext.ForReversal(transaction.SerialIds is { Count: > 0 });
        }
        else
        {
            MasterDataCheck check;
            try
            {
                (check, lotContext) = await _masterData.CheckWithLotsAsync(movement, cancellationToken);
            }
            catch (ProductMasterUnavailableException)
            {
                return Response<MovementResponseDto>.Fail(InventoryErrorCodes.ProductMasterUnavailable,
                    "PRODUCT-MASTER could not be reached; the movement was not posted.", 503);
            }
            catch (LocationMasterUnavailableException)
            {
                return Response<MovementResponseDto>.Fail(InventoryErrorCodes.LocationMasterUnavailable,
                    "LOCATION master could not be reached; the movement was not posted.", 503);
            }

            if (!check.IsValid)
            {
                await _expiry.FlushRefusalAuditAsync(cancellationToken); // MOD-0176 F2: a hook refusal is audited (no-op otherwise)
                return Response<MovementResponseDto>.Fail(check.ErrorCode!, check.Message!, 422, check.Details);
            }

            transaction = NewTransaction(movement, fingerprint, movement.Direction!, check.BaseQuantity, check.BaseUomId,
                movement.LotId, movement.SerialIds?.ToList(), reversesTransactionId: null);
            transaction.CreatedBy = actorUserId;
            transaction.CreatedByName = NameOf(actorUserId);
            transaction.ReservationId = reservationId; // validated (and consumed) inside the posting transaction
        }

        var posting = new LedgerPosting(
            transaction,
            LedgerLegs.For(transaction),
            NewAudit(transaction, _tenantContext.LegalEntitySource),
            NewOutbox(transaction),
            lotContext,
            restore);

        var result = await _ledger.PostAsync(posting, cancellationToken);
        switch (result.Outcome)
        {
            case LedgerPostingOutcome.Posted:
                await NotifyObserversAsync(result.Transaction!, cancellationToken);
                return Response<MovementResponseDto>.Success(
                    InventoryMapping.ToMovementResponse(result.Transaction!, idempotentReplay: false, _tenantContext.LegalEntityId), 201);

            case LedgerPostingOutcome.InsufficientStock when result.Level is not null:
            {
                // MOD-0173-FU03 §8.2 / AC-40 / AC-28: a reservation scope guard refused — reserved stock does not leave to someone else (nor
                // out of its warehouse). The same code (MOD-0175 / MVP-6 G7 map it), details.level added; one refusal row after the rollback,
                // with the reservationId text when the movement named one.
                var details = new Dictionary<string, object?>
                {
                    ["available"] = InventoryQuantity.ToWire(result.Available ?? 0m),
                    ["requested"] = InventoryQuantity.ToWire(result.Requested ?? 0m),
                    ["level"] = result.Level
                };
                return await RefuseReservationAsync(Facts(transaction, movement.ReservationId, actorUserId), InventoryErrorCodes.InsufficientStock,
                    "Insufficient unreserved stock: the quantity is reserved at this scope level.", details, result, cancellationToken,
                    existingReservation: reservationId);
            }

            case LedgerPostingOutcome.ReservationRefused:
                // MOD-0173-FU03 §8.3 step 5: the named reservation refused inside the transaction (NOT_FOUND / NOT_ACTIVE / MISMATCH /
                // QUANTITY_EXCEEDED); nothing written, one refusal row. A NOT_FOUND names no reservation of this scope.
                return await RefuseReservationAsync(Facts(transaction, movement.ReservationId, actorUserId), result.RefusalCode!, result.RefusalMessage!,
                    result.RefusalDetails, null, cancellationToken,
                    existingReservation: result.RefusalCode == InventoryErrorCodes.ReservationNotFound ? null : reservationId);

            case LedgerPostingOutcome.InsufficientStock:
                return Response<MovementResponseDto>.Fail(InventoryErrorCodes.InsufficientStock,
                    "Insufficient stock on the source row; negative stock is prohibited.", 422,
                    new Dictionary<string, object?>
                    {
                        ["available"] = InventoryQuantity.ToWire(result.Available ?? 0m),
                        ["requested"] = InventoryQuantity.ToWire(result.Requested ?? 0m)
                    });

            case LedgerPostingOutcome.AlreadyReversed:
                return AlreadyReversed();

            case LedgerPostingOutcome.LocationNotPostable:
                // MOD-0173-FU02 K-13: re-checked inside the posting transaction (REVERSAL included) — a location that is
                // not Active + stockable under the named warehouse at commit time is, for the ledger, unknown.
                return Response<MovementResponseDto>.Fail(InventoryErrorCodes.UnknownLocation,
                    "Location is not an Active stockable location of this company under the named warehouse.", 422,
                    new Dictionary<string, object?> { ["locationId"] = result.RefusedLocationId });

            case LedgerPostingOutcome.LotOrSerialRefused:
                // WP-0174-P2: a registered lot / a serial re-checked inside the posting transaction (e.g. the lot was
                // blocked between the pre-check and the commit) — pack §12 code, nothing written.
                await _expiry.FlushRefusalAuditAsync(cancellationToken); // MOD-0176 F2: an in-transaction hook refusal, audited after the rollback
                return Response<MovementResponseDto>.Fail(result.RefusalCode!, result.RefusalMessage!, 422, result.RefusalDetails);

            case LedgerPostingOutcome.IdempotencyKeyTaken:
            {
                // A concurrent request committed this key between our lookup and our insert.
                var winner = await _ledger.FindByIdempotencyKeyAsync(movement.IdempotencyKey, cancellationToken)
                    ?? throw new InvalidOperationException("Idempotency key reported taken but no committed transaction carries it.");
                return Replay(winner, fingerprint);
            }

            default:
                throw new InvalidOperationException($"Unhandled posting outcome {result.Outcome}.");
        }
    }

    /// <summary>FU03 §8.3: the types / directions that may name a reservation — the issues that consume it and the transfer that moves it.</summary>
    private static bool NamesReservation(ParsedMovement movement)
        => movement.MovementType is MovementType.GoodsIssueSales or MovementType.GoodsIssueProd or MovementType.LocationTransfer
           && movement.Direction?.FromStockStatus == StockStatus.Available;

    /// <summary>What a consumption refusal row says about the movement (§4.4): the request, or the transaction built from it.</summary>
    private sealed record RefusedMovement(
        Guid ItemId, Guid SkuId, SkuLevel SkuLevel, string? WarehouseId, string? LotText, MovementType MovementType, string SourceModule,
        string? ReservationIdText, Guid ActorUserId, Guid CorrelationId);

    private RefusedMovement Facts(ParsedMovement movement, string? reservationIdText, Guid actorUserId)
        => new(movement.ItemId, movement.SkuId, movement.SkuLevel, movement.Direction?.FromWarehouseId, movement.LotId, movement.MovementType,
            movement.SourceModule, reservationIdText, actorUserId, _correlation.CorrelationId);

    private static RefusedMovement Facts(InventoryTransaction transaction, string? reservationIdText, Guid actorUserId)
        => new(transaction.ItemId, transaction.SkuId, transaction.SkuLevel, transaction.FromWarehouseId, transaction.LotId, transaction.MovementType,
            transaction.SourceModule, reservationIdText, actorUserId, transaction.CorrelationId);

    /// <summary>
    /// MOD-0173-FU03 §4.4 / §21 (AC-28, AC-40): ONE <c>InventoryReservationConsumptionRefused</c> row for a movement refused because of a
    /// reservation — a RESERVATION_* refusal or a scope guard's INSUFFICIENT_STOCK — written after the rolled-back posting, best effort (the
    /// repository logs and counts a failure), then the 422. <c>ReservationIdText</c> is the text as sent (≤ 100, null when none);
    /// <c>ReservationId</c> the reservation of this scope it named, else <see cref="Guid.Empty"/>. A row-level refusal (negative-stock guard, no
    /// level) is not this and writes none.
    /// </summary>
    private async Task<Response<MovementResponseDto>> RefuseReservationAsync(
        RefusedMovement facts, string code, string message, IReadOnlyDictionary<string, object?>? details, LedgerPostingResult? guard,
        CancellationToken ct, Guid? existingReservation = null)
    {
        if (_reservations is not null)
        {
            var now = DateTimeOffset.UtcNow;
            await _reservations.WriteRefusalAsync(new InventoryReservationAuditRecord
            {
                Id = Guid.NewGuid(),
                CreatedAt = now,
                Action = ReservationAuditActions.ConsumptionRefused,
                ReservationId = existingReservation ?? Guid.Empty,
                ReservationIdText = facts.ReservationIdText is { Length: > 100 } longText ? longText[..100] : facts.ReservationIdText,
                ItemId = facts.ItemId,
                SkuId = facts.SkuId,
                SkuLevel = facts.SkuLevel,
                WarehouseId = facts.WarehouseId,
                LotId = LotPostingRules.IsRegisteredLotForm(facts.LotText, out var lot) ? lot : null,
                ChangedQuantity = 0m,
                RemainingQuantity = 0m,
                Status = null,
                Outcome = ReservationAuditOutcomes.Failed,
                ErrorCode = code,
                Level = guard?.Level,
                MovementType = WireNames.ToWire(facts.MovementType),
                RequestedQuantity = guard?.Requested,
                UncoveredQuantity = guard?.Available,
                SourceModule = facts.SourceModule,
                ActorUserId = facts.ActorUserId,
                ActorDisplayName = NameOf(facts.ActorUserId),
                CorrelationId = facts.CorrelationId,
                LeSource = _tenantContext.LegalEntitySource,
                OccurredAt = now
            }, ct);
        }

        return Response<MovementResponseDto>.Fail(code, message, 422, details);
    }

    private async Task NotifyObserversAsync(InventoryTransaction transaction, CancellationToken ct)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await observer.OnPostedAsync(transaction, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Posted-movement observer {Observer} failed for transaction {TransactionId}; the posting stands.", observer.GetType().Name, transaction.Id);
            }
        }
    }

    private Response<MovementResponseDto> Replay(InventoryTransaction committed, string fingerprint)
    {
        if (!string.Equals(committed.RequestFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return Response<MovementResponseDto>.Fail(InventoryErrorCodes.IdempotencyKeyReused,
                "This idempotency key was already used for a different movement.", 400);
        }

        return Response<MovementResponseDto>.Success(
            InventoryMapping.ToMovementResponse(committed, idempotentReplay: true, _tenantContext.LegalEntityId), 200);
    }

    private async Task<(InventoryTransaction? Reversal, Response<MovementResponseDto>? Refusal, InventoryTransaction? Original)> BuildReversalAsync(
        ParsedMovement movement, string fingerprint, CancellationToken ct)
    {
        var (originalId, envelopeError) = ReversalRules.ReadOriginalReference(
            movement.SourceModule, movement.SourceType, movement.SourceDocumentId);
        if (envelopeError is not null)
        {
            return (null, Response<MovementResponseDto>.Fail(InventoryErrorCodes.InvalidRequest, envelopeError, 400), null);
        }

        // Scoped lookup: another tenant's / legal entity's transaction is simply not found.
        var original = await _ledger.FindTransactionAsync(originalId, ct);
        if (original is null)
        {
            return (null, Response<MovementResponseDto>.Fail(InventoryErrorCodes.InvalidRequest,
                "REVERSAL sourceDocumentId does not name a transaction of this tenant and legal entity.", 400), null);
        }

        var mismatch = ReversalRules.MatchOriginal(original, movement.ItemId, movement.SkuId, movement.SkuLevel,
            movement.Quantity, movement.UomId);
        if (mismatch is not null)
        {
            return (null, Response<MovementResponseDto>.Fail(InventoryErrorCodes.InvalidRequest, mismatch, 400), null);
        }

        if (await _ledger.IsReversedAsync(originalId, ct))
        {
            return (null, AlreadyReversed(), null);
        }

        var inverse = MovementDirectionRules.InverseOf(ReversalRules.DirectionOf(original));

        // WP-0174-P4 (CT K4-3): an original whose typed lot the migration linked is reversed through its REGISTERED lot —
        // its balance row was re-keyed to that lot (CT-D6); the original document stays as it is (append-only).
        var lotId = original.LotId is { } typed && !LotPostingRules.IsRegisteredLotForm(typed, out _)
            ? await _lots.LinkedLotIdAsync(original.Id, ct) ?? typed
            : original.LotId;
        var reversal = NewTransaction(movement, fingerprint, inverse, original.BaseQuantity, original.BaseUomId,
            lotId, original.SerialIds?.ToList(), reversesTransactionId: original.Id);
        return (reversal, null, original);
    }

    private static Response<MovementResponseDto> AlreadyReversed()
        => Response<MovementResponseDto>.Fail(InventoryErrorCodes.AlreadyReversed,
            "The original transaction has already been reversed.", 422);

    private InventoryTransaction NewTransaction(
        ParsedMovement movement,
        string fingerprint,
        MovementDirection direction,
        decimal baseQuantity,
        string baseUomId,
        string? lotId,
        List<string>? serialIds,
        Guid? reversesTransactionId) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = DateTimeOffset.UtcNow,
        DocumentGroupId = movement.DocumentGroupId,
        MovementType = movement.MovementType,
        MovementReason = movement.MovementReason,
        ItemId = movement.ItemId,
        SkuId = movement.SkuId,
        SkuLevel = movement.SkuLevel,
        FromWarehouseId = direction.FromWarehouseId,
        FromLocationId = direction.FromLocationId,
        FromStockStatus = direction.FromStockStatus,
        ToWarehouseId = direction.ToWarehouseId,
        ToLocationId = direction.ToLocationId,
        ToStockStatus = direction.ToStockStatus,
        Quantity = movement.Quantity,
        UomId = movement.UomId,
        BaseQuantity = baseQuantity,
        BaseUomId = baseUomId,
        LotId = lotId,
        SerialIds = serialIds,
        SourceModule = movement.SourceModule,
        SourceType = movement.SourceType,
        SourceDocumentId = movement.SourceDocumentId,
        SourceLineId = movement.SourceLineId,
        SourceSystem = movement.SourceSystem,
        ExternalRef = null,
        ReversesTransactionId = reversesTransactionId,
        TransactionDate = movement.TransactionDate,
        PostingDate = movement.PostingDate,
        IdempotencyKey = movement.IdempotencyKey,
        RequestFingerprint = fingerprint,
        CorrelationId = _correlation.CorrelationId,
        CreatedBy = _tenantContext.UserId
    };

    private static InventoryAuditRecord NewAudit(InventoryTransaction transaction, string leSource) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = transaction.CreatedAt,
        Action = transaction.MovementType == MovementType.Reversal ? ReversedAction : PostedAction,
        TransactionId = transaction.Id,
        MovementType = transaction.MovementType,
        ItemId = transaction.ItemId,
        SkuId = transaction.SkuId,
        BaseQuantity = transaction.BaseQuantity,
        BaseUomId = transaction.BaseUomId,
        ActorUserId = transaction.CreatedBy,
        CorrelationId = transaction.CorrelationId,
        IdempotencyKey = transaction.IdempotencyKey,
        SourceModule = transaction.SourceModule,
        SourceDocumentId = transaction.SourceDocumentId,
        SourceSystem = transaction.SourceSystem,
        LeSource = leSource,
        OccurredAt = transaction.CreatedAt
    };

    private static InventoryOutboxMessage NewOutbox(InventoryTransaction transaction) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = transaction.CreatedAt,
        AggregateId = transaction.Id,
        CorrelationId = transaction.CorrelationId,
        OccurredAt = transaction.CreatedAt,
        Payload = new InventoryMovementPostedPayload
        {
            TransactionId = transaction.Id,
            MovementType = transaction.MovementType,
            ItemId = transaction.ItemId,
            SkuId = transaction.SkuId,
            SkuLevel = transaction.SkuLevel,
            FromWarehouseId = transaction.FromWarehouseId,
            FromLocationId = transaction.FromLocationId,
            ToWarehouseId = transaction.ToWarehouseId,
            ToLocationId = transaction.ToLocationId,
            LotId = transaction.LotId,
            FromStockStatus = transaction.FromStockStatus,
            ToStockStatus = transaction.ToStockStatus,
            BaseQuantity = transaction.BaseQuantity,
            BaseUomId = transaction.BaseUomId,
            ReversesTransactionId = transaction.ReversesTransactionId,
            PostingDate = transaction.PostingDate
        }
    };
}
