using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Handlers.CommandHandlers;

/// <summary>
/// postMovement. Order matters and is the whole design:
/// <list type="number">
/// <item>Idempotency first — a key already committed in this Tenant + LE answers from the durable record: same
/// fingerprint → 200 replay (no upstream call, no write); different fingerprint → 400 IDEMPOTENCY_KEY_REUSED.</item>
/// <item>REVERSAL copies the original (ASSUMPTION-0173-05); any other type is checked against PRODUCT-MASTER and
/// LOCATION — unreachable → 503, nothing written.</item>
/// <item>One atomic posting (transaction + balance legs + audit + Pending outbox). A concurrent winner on the same
/// key or the same original is resolved from the committed record, never by writing twice.</item>
/// </list>
/// </summary>
public sealed class PostMovementHandler : IRequestHandler<PostMovementCommand, Response<MovementResponseDto>>
{
    private const string PostedAction = "InventoryMovementPosted";
    private const string ReversedAction = "InventoryMovementReversed";

    private readonly IInventoryLedgerRepository _ledger;
    private readonly MovementMasterDataValidator _masterData;
    private readonly ITenantContext _tenantContext;
    private readonly ICorrelationContext _correlation;

    public PostMovementHandler(
        IInventoryLedgerRepository ledger,
        MovementMasterDataValidator masterData,
        ITenantContext tenantContext,
        ICorrelationContext correlation)
    {
        _ledger = ledger;
        _masterData = masterData;
        _tenantContext = tenantContext;
        _correlation = correlation;
    }

    public async Task<Response<MovementResponseDto>> Handle(PostMovementCommand request, CancellationToken cancellationToken)
    {
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
        if (movement.MovementType == MovementType.Reversal)
        {
            var (reversal, refusal) = await BuildReversalAsync(movement, fingerprint, cancellationToken);
            if (refusal is not null)
            {
                return refusal;
            }

            transaction = reversal!;
        }
        else
        {
            MasterDataCheck check;
            try
            {
                check = await _masterData.CheckAsync(movement, cancellationToken);
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
        }

        var posting = new LedgerPosting(
            transaction,
            LedgerLegs.For(transaction),
            NewAudit(transaction, _tenantContext.LegalEntitySource),
            NewOutbox(transaction));

        var result = await _ledger.PostAsync(posting, cancellationToken);
        switch (result.Outcome)
        {
            case LedgerPostingOutcome.Posted:
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
        var reversal = NewTransaction(movement, fingerprint, inverse, original.BaseQuantity, original.BaseUomId,
            original.LotId, original.SerialIds?.ToList(), reversesTransactionId: original.Id);
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
