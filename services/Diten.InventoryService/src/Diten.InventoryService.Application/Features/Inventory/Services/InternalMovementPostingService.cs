using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Ports;
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

    public InternalMovementPostingService(
        IInventoryLedgerRepository ledger,
        ILotMasterClient lots,
        MovementMasterDataValidator masterData,
        ITenantContext tenantContext,
        ICorrelationContext correlation,
        IEnumerable<IPostedMovementObserver> observers,
        ILogger<InternalMovementPostingService> logger)
    {
        _ledger = ledger;
        _lots = lots;
        _masterData = masterData;
        _tenantContext = tenantContext;
        _correlation = correlation;
        _observers = observers;
        _logger = logger;
    }

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

        InventoryTransaction transaction;
        LotPostingContext? lotContext;
        if (movement.MovementType == MovementType.Reversal)
        {
            var (reversal, refusal) = await BuildReversalAsync(movement, fingerprint, cancellationToken);
            if (refusal is not null)
            {
                return refusal;
            }

            transaction = reversal!;
            transaction.CreatedBy = actorUserId;
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
                return Response<MovementResponseDto>.Fail(check.ErrorCode!, check.Message!, 422, check.Details);
            }

            transaction = NewTransaction(movement, fingerprint, movement.Direction!, check.BaseQuantity, check.BaseUomId,
                movement.LotId, movement.SerialIds?.ToList(), reversesTransactionId: null);
            transaction.CreatedBy = actorUserId;
        }

        var posting = new LedgerPosting(
            transaction,
            LedgerLegs.For(transaction),
            NewAudit(transaction, _tenantContext.LegalEntitySource),
            NewOutbox(transaction),
            lotContext);

        var result = await _ledger.PostAsync(posting, cancellationToken);
        switch (result.Outcome)
        {
            case LedgerPostingOutcome.Posted:
                await NotifyObserversAsync(result.Transaction!, cancellationToken);
                return Response<MovementResponseDto>.Success(
                    InventoryMapping.ToMovementResponse(result.Transaction!, idempotentReplay: false, _tenantContext.LegalEntityId), 201);

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

    private async Task<(InventoryTransaction? Reversal, Response<MovementResponseDto>? Refusal)> BuildReversalAsync(
        ParsedMovement movement, string fingerprint, CancellationToken ct)
    {
        var (originalId, envelopeError) = ReversalRules.ReadOriginalReference(
            movement.SourceModule, movement.SourceType, movement.SourceDocumentId);
        if (envelopeError is not null)
        {
            return (null, Response<MovementResponseDto>.Fail(InventoryErrorCodes.InvalidRequest, envelopeError, 400));
        }

        // Scoped lookup: another tenant's / legal entity's transaction is simply not found.
        var original = await _ledger.FindTransactionAsync(originalId, ct);
        if (original is null)
        {
            return (null, Response<MovementResponseDto>.Fail(InventoryErrorCodes.InvalidRequest,
                "REVERSAL sourceDocumentId does not name a transaction of this tenant and legal entity.", 400));
        }

        var mismatch = ReversalRules.MatchOriginal(original, movement.ItemId, movement.SkuId, movement.SkuLevel,
            movement.Quantity, movement.UomId);
        if (mismatch is not null)
        {
            return (null, Response<MovementResponseDto>.Fail(InventoryErrorCodes.InvalidRequest, mismatch, 400));
        }

        if (await _ledger.IsReversedAsync(originalId, ct))
        {
            return (null, AlreadyReversed());
        }

        var inverse = MovementDirectionRules.InverseOf(ReversalRules.DirectionOf(original));

        // WP-0174-P4 (CT K4-3): an original whose typed lot the migration linked is reversed through its REGISTERED lot —
        // its balance row was re-keyed to that lot (CT-D6); the original document stays as it is (append-only).
        var lotId = original.LotId is { } typed && !LotPostingRules.IsRegisteredLotForm(typed, out _)
            ? await _lots.LinkedLotIdAsync(original.Id, ct) ?? typed
            : original.LotId;
        var reversal = NewTransaction(movement, fingerprint, inverse, original.BaseQuantity, original.BaseUomId,
            lotId, original.SerialIds?.ToList(), reversesTransactionId: original.Id);
        return (reversal, null);
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
