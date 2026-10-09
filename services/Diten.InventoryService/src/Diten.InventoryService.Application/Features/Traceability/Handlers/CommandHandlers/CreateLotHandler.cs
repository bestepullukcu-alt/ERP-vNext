using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Traceability.Commands;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Domain.Traceability.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Handlers.CommandHandlers;

/// <summary>
/// createLot (TRACE v1 + v1.1; pack §12 rows 1–9). Order:
/// <list type="number">
/// <item>Shape (400): idempotency key, itemId, lot number (<c>LOT_NUMBER_INVALID</c>), attribute lengths, country,
/// dates (<c>LOT_DATES_INVALID</c>, also the date order rules).</item>
/// <item>SKU present (B-2 = CT-D2): no <c>skuId</c> + <c>skuLevel</c> → 422 <c>LOT_SKU_REQUIRED</c>.</item>
/// <item>Idempotency: a committed key answers from the stored lot — same fingerprint → 200, else 400.</item>
/// <item>PRODUCT-MASTER, before any write (fail-closed 503): the SKU exists (422 <c>UNKNOWN_ITEM</c>), it links to
/// <c>itemId</c> (422 <c>LOT_SKU_MISMATCH</c>), the product is lot-controlled (422 <c>PRODUCT_NOT_LOT_CONTROLLED</c>).</item>
/// <item>One transaction: the K-G key (built by <see cref="LotIdentityPolicy"/>) is free (409 <c>LOT_ALREADY_EXISTS</c>,
/// also enforced by the unique index), insert as <c>Draft</c> + one <c>Created</c> history row.</item>
/// </list>
/// </summary>
public sealed class CreateLotHandler : IRequestHandler<CreateLotCommand, Response<LotRefDto>>
{
    private readonly ILotRepository _lots;
    private readonly IProductMasterClient _products;
    private readonly ITraceSkuDirectory _skus;
    private readonly TraceWriteSupport _support;
    private readonly Common.ITenantContext _tenant;

    public CreateLotHandler(
        ILotRepository lots,
        IProductMasterClient products,
        ITraceSkuDirectory skus,
        TraceWriteSupport support,
        Common.ITenantContext tenant)
    {
        _lots = lots;
        _products = products;
        _skus = skus;
        _support = support;
        _tenant = tenant;
    }

    public async Task<Response<LotRefDto>> Handle(CreateLotCommand request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var today = TraceMapping.Today(now);

        var keyError = TraceWriteSupport.KeyError(request.HeaderIdempotencyKey, request.IdempotencyKey, out var key);
        if (keyError is not null) return TraceWriteSupport.Invalid<LotRefDto>(keyError);
        if (!TraceWriteSupport.TryParseId(request.ItemId, out var itemId))
            return TraceWriteSupport.Invalid<LotRefDto>("itemId is required and must be a UUID.");

        var lotNumber = request.LotNumber is null ? null : LotIdentityPolicy.NormalizeLotNumber(request.LotNumber);
        if (!LotIdentityPolicy.IsValidLotNumber(lotNumber))
            return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.LotNumberInvalid,
                $"lotNumber is required: 1-{LotIdentityPolicy.MaxLotNumberLength} characters of A-Z 0-9 . / - (trimmed, upper-cased).", 400);

        var attributes = LotAttributes.Read(request.ManufacturerLot, request.SupplierLot, request.CountryOfOrigin, request.CoaReference);
        if (attributes.Error is not null) return TraceWriteSupport.Invalid<LotRefDto>(attributes.Error);

        var dates = LotAttributes.ReadDates(request.ManufacturingDate, request.ExpiryDate, request.RetestDate, today);
        if (dates.Error is not null) return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.LotDatesInvalid, dates.Error, 400);

        if (string.IsNullOrWhiteSpace(request.SkuId) || string.IsNullOrWhiteSpace(request.SkuLevel))
            return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.LotSkuRequired,
                "A lot belongs to one SKU: skuId and skuLevel are required.", 422);
        if (!TraceWriteSupport.TryParseId(request.SkuId, out var skuId))
            return TraceWriteSupport.Invalid<LotRefDto>("skuId must be a UUID.");
        if (!WireNames.TryParseLegacySkuLevel(request.SkuLevel, out var skuLevel))
            return TraceWriteSupport.Invalid<LotRefDto>("skuLevel must be Gsku, Lsku or FinishedGood.");
        if (!LotIdentityPolicy.MayBindTo(skuLevel))
            return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.LotSkuMismatch, $"A lot cannot be bound to a {request.SkuLevel} SKU.", 422);

        var fingerprint = TraceWriteSupport.Fingerprint(
            ("itemId", TraceMapping.Id(itemId)), ("lotNumber", lotNumber), ("skuId", TraceMapping.Id(skuId)),
            ("skuLevel", WireNames.ToWire(skuLevel)), ("manufacturerLot", attributes.ManufacturerLot),
            ("supplierLot", attributes.SupplierLot), ("countryOfOrigin", attributes.Country), ("coaReference", attributes.Coa),
            ("manufacturingDate", TraceMapping.Wire(dates.Manufacturing)), ("expiryDate", TraceMapping.Wire(dates.Expiry)),
            ("retestDate", TraceMapping.Wire(dates.Retest)));

        var committed = await _lots.FindByCreateKeyAsync(key, cancellationToken);
        if (committed is not null) return Replay(committed, fingerprint, today);

        TraceSku? sku;
        ProductReference? product;
        try
        {
            sku = await _skus.GetSkuAsync(skuId, skuLevel, cancellationToken);
            product = sku is null ? null : await _products.GetProductAsync(itemId, cancellationToken);
        }
        catch (ProductMasterUnavailableException)
        {
            return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.ProductMasterUnavailable,
                "PRODUCT-MASTER could not confirm the SKU; nothing was saved.", 503);
        }

        if (sku is null)
            return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.UnknownItem, "The SKU does not exist in PRODUCT-MASTER.", 422);
        if (sku.ItemId != itemId)
            return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.LotSkuMismatch, "The SKU does not belong to itemId (D-SKU-LINK).", 422);
        if (product is null)
            return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.UnknownItem, "The item does not exist in PRODUCT-MASTER.", 422);
        // WP-0173-PM1 (OD-1, OD-5): the lot flag is the SKU's (getSku v1.1). Unknown → refused, never "not lot-controlled";
        // TRACE 1.2.0 declares no "data incomplete" code, so the refusal is the contract's 503 PRODUCT_MASTER_UNAVAILABLE
        // (ASSUMPTION-0173-PM1-03, open point O-PM1-1).
        if (sku.LotControlled is not { } skuLotControlled)
            return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.ProductMasterUnavailable,
                "PRODUCT-MASTER does not state whether the SKU is lot-controlled; nothing was saved.", 503);
        if (!skuLotControlled)
            return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.ProductNotLotControlled, "The product is not lot-controlled.", 422);

        Response<LotRefDto> outcome;
        try
        {
            outcome = await _lots.InTransactionAsync(async (tx, ct) =>
            {
                if (await tx.FindByIdentityKeyAsync(LotIdentityPolicy.LotKey(_tenant.TenantId, skuId, skuLevel, lotNumber!), ct) is not null)
                    return AlreadyExists(lotNumber!);

                var lot = new Lot
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = now,
                    ItemId = itemId,
                    SkuId = skuId,
                    SkuLevel = skuLevel,
                    LotNumber = lotNumber!,
                    IdentityKey = LotIdentityPolicy.LotKey(_tenant.TenantId, skuId, skuLevel, lotNumber!),
                    ManufacturerLot = attributes.ManufacturerLot,
                    SupplierLot = attributes.SupplierLot,
                    ManufacturingDate = dates.Manufacturing,
                    ExpiryDate = dates.Expiry,
                    RetestDate = dates.Retest,
                    CountryOfOrigin = attributes.Country,
                    CoaReference = attributes.Coa,
                    Status = LotStatus.Draft,
                    // WP-0173-PM1: the SKU's serial flag (GSKU: the product's, OD-10); unknown → Lot + Serial, never Lot only.
                    TrackingMode = ProductMasterStockRules.LotNeedsSerials(skuLevel, sku.SerialControlled, product.SerialControlled)
                        ? LotTrackingMode.LotAndSerial : LotTrackingMode.Lot,
                    CreationSource = "Api",
                    CreatedBy = _support.UserId,
                    Version = 1,
                    CreateIdempotencyKey = key,
                    CreateRequestFingerprint = fingerprint
                };

                await tx.InsertAsync(lot, ct);
                await tx.AppendHistoryAsync([_support.History(lot.Id, LotChangeAction.Created, null, null, lot.LotNumber, null, now)], ct);
                return Response<LotRefDto>.Success(TraceMapping.ToRef(lot, today, withETag: true), 201);
            }, cancellationToken);
        }
        catch (LotUniqueViolationException violation) when (violation.Index == LotUniqueIndex.Identity)
        {
            outcome = AlreadyExists(lotNumber!);
        }
        catch (LotUniqueViolationException violation) when (violation.Index == LotUniqueIndex.CreateKey)
        {
            var winner = await _lots.FindByCreateKeyAsync(key, cancellationToken)
                         ?? throw new InvalidOperationException("Create key reported taken but no lot carries it.");
            return Replay(winner, fingerprint, today);
        }

        // A concurrent request with the SAME key may have committed this very lot first: that is a replay, not a clash.
        if (outcome.ErrorCode == TraceErrorCodes.LotAlreadyExists
            && await _lots.FindByCreateKeyAsync(key, cancellationToken) is { } sameKey)
        {
            return Replay(sameKey, fingerprint, today);
        }

        return outcome;
    }

    private static Response<LotRefDto> Replay(Lot committed, string fingerprint, DateOnly today)
        => string.Equals(committed.CreateRequestFingerprint, fingerprint, StringComparison.Ordinal)
            ? Response<LotRefDto>.Success(TraceMapping.ToRef(committed, today, withETag: true), 200)
            : TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.IdempotencyKeyReused,
                "This idempotency key was already used for a different lot.", 400);

    private static Response<LotRefDto> AlreadyExists(string lotNumber)
        => TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.LotAlreadyExists,
            $"Lot '{lotNumber}' already exists for this SKU.", 409);
}
