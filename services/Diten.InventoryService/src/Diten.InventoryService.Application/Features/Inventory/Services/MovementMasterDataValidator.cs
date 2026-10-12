using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Traceability;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Rules;
using Microsoft.Extensions.Options;

namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>
/// Pack §12 rules that need the frozen upstream masters (PRODUCT-MASTER, LOCATION), in this order:
/// item / sku existence (<c>UNKNOWN_ITEM</c>) → D-SKU-LINK (<c>SKU_ITEM_MISMATCH</c>) → base-UoM conversion
/// (<c>UOM_CONVERSION_MISSING</c>) → lot (<c>LOT_REQUIRED</c>) → serial count (<c>SERIAL_COUNT_MISMATCH</c>) →
/// each named location exists, is Active and stockable, belongs to the caller's legal entity and sits under the
/// named warehouse (<c>UNKNOWN_LOCATION</c>).
/// Upstream failures are not caught here: they surface as the *UnavailableException of the client and the handler
/// turns them into 503 before anything is written (fail-closed, pack §8).
/// <para>
/// WP-0174-P2 (MOD-0174 pack §12 "Ledger posting rules") — the lot / serial rules are inserted in the pack's order without
/// moving any existing one: after <c>LOT_REQUIRED</c> come <c>LOT_NOT_ALLOWED</c> → <c>UNKNOWN_LOT</c> →
/// <c>LOT_SKU_MISMATCH</c> → <c>LOT_BLOCKED</c> → <c>LOT_EXPIRED</c>; after <c>SERIAL_COUNT_MISMATCH</c> come
/// <c>SERIAL_ALREADY_EXISTS</c> → <c>SERIAL_NOT_AVAILABLE</c> → <c>SERIAL_LOT_MISMATCH</c>; locations last as before. The
/// lot / serial source is the in-process <see cref="ILotMasterClient"/>; the posting transaction re-checks the same rules
/// on the committed state. A typed (unregistered) lot keeps its pre-P2 behaviour while <c>Trace:EnforceRegisteredLots</c>
/// is off (<see cref="LotLedgerOptions"/>).
/// </para>
/// <para>
/// WP-0174-P4 (CT K4-1 / K4-4, ASSUMPTION-0174-10 as revised): once the tenant's typed-lot migration closed typing
/// (<c>ManualLotEntryClosed</c>), an INBOUND movement of a LOT-CONTROLLED item is accepted only with a registered lot
/// (<c>UNKNOWN_LOT</c>) — the same rule the global switch <c>Trace:EnforceRegisteredLots</c> applies, which stays as an
/// override (default off). Outbound / transfer movements and items that are not lot-controlled keep today's behaviour,
/// so stock that still carries a typed text (not lot-controlled, never migrated) can leave. The posting transaction
/// re-applies the inbound rule on the committed state (<see cref="TypedLotPostingGuard"/>).
/// </para>
/// </summary>
public sealed class MovementMasterDataValidator
{
    private readonly IProductMasterClient _productMaster;
    private readonly ILocationMasterClient _locationMaster;
    private readonly ILotMasterClient _lotMaster;
    private readonly ITenantContext _tenantContext;
    private readonly InventoryPolicyOptions _policy;
    private readonly LotLedgerOptions _lotPolicy;
    private readonly TypedLotPostingGuard _typedGuard;
    private readonly Expiry.Ports.IExpiryPostingPolicy _expiry;

    public MovementMasterDataValidator(
        IProductMasterClient productMaster,
        ILocationMasterClient locationMaster,
        ILotMasterClient lotMaster,
        ITenantContext tenantContext,
        IOptions<InventoryPolicyOptions> policy,
        IOptions<LotLedgerOptions> lotPolicy,
        TypedLotPostingGuard typedGuard,
        Expiry.Ports.IExpiryPostingPolicy expiry)
    {
        _expiry = expiry;
        _productMaster = productMaster;
        _locationMaster = locationMaster;
        _lotMaster = lotMaster;
        _tenantContext = tenantContext;
        _policy = policy.Value;
        _lotPolicy = lotPolicy.Value;
        _typedGuard = typedGuard;
    }

    public async Task<MasterDataCheck> CheckAsync(ParsedMovement movement, CancellationToken ct)
        => (await CheckWithLotsAsync(movement, ct)).Check;

    /// <summary>
    /// <see cref="CheckAsync"/> plus what the posting transaction must do with the movement's serials (WP-0174-P2); the
    /// context is null when the check refused.
    /// </summary>
    public async Task<(MasterDataCheck Check, LotPostingContext? Lot)> CheckWithLotsAsync(ParsedMovement movement, CancellationToken ct)
    {
        var decided = new LotDecision();
        var check = await CheckCoreAsync(movement, decided, ct);
        return (check, check.IsValid ? decided.Context : null);
    }

    private async Task<MasterDataCheck> CheckCoreAsync(ParsedMovement movement, LotDecision decided, CancellationToken ct)
    {
        var direction = movement.Direction
            ?? throw new InvalidOperationException("A REVERSAL is not validated against the masters; it copies the original.");

        // ── PRODUCT-MASTER ────────────────────────────────────────────────────────────────────────────────
        var validation = await _productMaster.ValidateRefsAsync(
        [
            new ProductRefQuery(movement.ItemId, null, null),
            new ProductRefQuery(null, movement.SkuId, movement.SkuLevel)
        ], ct);

        var itemKnown = validation.Any(r => r.Exists && SameId(r.ItemId, movement.ItemId) && r.SkuId is null);
        var skuKnown = validation.Any(r => r.Exists && SameId(r.SkuId, movement.SkuId)
                                           && string.Equals(r.SkuLevel, WireNames.ToWire(movement.SkuLevel), StringComparison.Ordinal));
        if (!itemKnown || !skuKnown)
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.UnknownItem,
                !itemKnown ? "itemId is not known to PRODUCT-MASTER." : "skuId/skuLevel is not known to PRODUCT-MASTER.");
        }

        var sku = await _productMaster.GetSkuAsync(movement.SkuId, movement.SkuLevel, ct);
        if (sku is null)
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.UnknownItem, "skuId/skuLevel is not known to PRODUCT-MASTER.");
        }

        if (!SameId(sku.ItemId, movement.ItemId))
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.SkuItemMismatch,
                "The SKU does not belong to itemId (D-SKU-LINK).",
                new Dictionary<string, object?> { ["skuItemId"] = sku.ItemId, ["itemId"] = movement.ItemId.ToString() });
        }

        // ── WP-0173-PM1: lifecycle (OD-3 / OD-4 / CT-PM1-2) — the most restrictive of the item row, the SKU row and the SKU ──
        var itemRow = validation.First(r => r.Exists && SameId(r.ItemId, movement.ItemId) && r.SkuId is null);
        var skuRow = validation.First(r => r.Exists && SameId(r.SkuId, movement.SkuId));
        var lifecycle = ProductMasterStockRules.MostRestrictive([itemRow.LifecycleStatus, skuRow.LifecycleStatus, sku.LifecycleStatus]);
        if (!ProductMasterStockRules.Allows(lifecycle, movement.MovementType))
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.ItemNotActive,
                $"The item / SKU lifecycle status does not allow a {WireNames.ToWire(movement.MovementType)} movement.",
                new Dictionary<string, object?>
                {
                    ["itemLifecycleStatus"] = itemRow.LifecycleStatus, ["skuLifecycleStatus"] = sku.LifecycleStatus ?? skuRow.LifecycleStatus,
                    ["movementType"] = WireNames.ToWire(movement.MovementType)
                });
        }

        // ── WP-0174-ITEM (O-PM1-3): an explicit stockable: false takes no stock at any level ──
        if (!ProductMasterStockRules.IsStockable(sku.Stockable))
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.SkuLevelNotStockable,
                "PRODUCT-MASTER publishes this SKU as not stockable (stockable: false); it takes no stock movement.",
                new Dictionary<string, object?> { ["skuLevel"] = WireNames.ToWire(movement.SkuLevel), ["stockable"] = false });
        }

        // ── WP-0173-PM1: lot / serial flags from getSku (OD-1); unknown → refused (OD-5); GSKU serial = product's (OD-10) ──
        var productSerial = movement.SkuLevel == SkuLevel.Gsku
            ? (await _productMaster.GetProductAsync(movement.ItemId, ct) ?? throw new ProductMasterUnavailableException(
                "PRODUCT-MASTER confirmed the item in validateRefs but getProduct answered 404.")).SerialControlled
            : null;
        var behaviour = ProductMasterStockRules.Behaviour(movement.SkuLevel, sku.LotControlled, sku.SerialControlled, productSerial);
        if (behaviour.Refusal is { } behaviourRefusal)
        {
            return behaviourRefusal == StockBehaviourRefusal.LevelNotStockable
                ? MasterDataCheck.Refused(InventoryErrorCodes.SkuLevelNotStockable,
                    "A GSKU-level movement needs a product whose serial flag is explicitly false; serial-tracked stock is held at Lsku / FinishedGood.",
                    new Dictionary<string, object?> { ["skuLevel"] = WireNames.ToWire(movement.SkuLevel), ["productSerialControlled"] = productSerial })
                : MasterDataCheck.Refused(InventoryErrorCodes.ProductMasterDataIncomplete,
                    "PRODUCT-MASTER does not state the lot / serial flag this SKU level needs; the movement cannot be checked.",
                    new Dictionary<string, object?>
                    {
                        ["skuLevel"] = WireNames.ToWire(movement.SkuLevel), ["lotControlled"] = sku.LotControlled, ["serialControlled"] = sku.SerialControlled
                    });
        }

        var lotControlled = behaviour.LotControlled;
        var serialControlled = behaviour.SerialControlled;

        var uom = await _productMaster.GetSkuUomAsync(movement.SkuId, ct);
        if (uom is null || string.IsNullOrWhiteSpace(uom.BaseUomId)
            || !UomConverter.TryConvertToBase(movement.Quantity, movement.UomId, uom.BaseUomId, uom.Conversions,
                movement.TransactionDate, out var baseQuantity))
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.UomConversionMissing,
                $"No conversion from '{movement.UomId}' to the SKU base UoM.",
                new Dictionary<string, object?> { ["uomId"] = movement.UomId, ["baseUomId"] = uom?.BaseUomId });
        }

        if (lotControlled && movement.LotId is null)
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.LotRequired, "lotId is required: the item is lot-controlled.");
        }

        // ── LOT (WP-0174-P2) ──────────────────────────────────────────────────────────────────────────────
        var enforce = _lotPolicy.EnforceRegisteredLots;
        Lot? lot = null;
        Domain.Expiry.Rules.ExpiryPostingInput? expiryInput = null;
        if (movement.LotId is { } lotId)
        {
            var registeredForm = LotPostingRules.IsRegisteredLotForm(lotId, out _);
            if (!lotControlled && (registeredForm || enforce))
            {
                return MasterDataCheck.Refused(InventoryErrorCodes.LotNotAllowed,
                    "lotId is not allowed: the item is not lot-controlled.", new Dictionary<string, object?> { ["lotId"] = lotId });
            }

            if (registeredForm)
            {
                lot = await _lotMaster.ResolveLotAsync(lotId, ct);
                if (lot is null)
                {
                    return MasterDataCheck.Refused(InventoryErrorCodes.UnknownLot,
                        "lotId does not name a lot of this tenant.", new Dictionary<string, object?> { ["lotId"] = lotId });
                }

                var violation = LotPostingRules.Evaluate(lot, movement.SkuId, movement.SkuLevel, movement.MovementType, movement.PostingDate,
                    direction.FromStockStatus, direction.ToStockStatus);
                if (violation is { } v)
                {
                    var (code, message, details) = v == LotPostingViolation.RetestDue ? InventoryErrorCodes.RetestDue(lot) : LotPostingErrors.For(v, lot);
                    return MasterDataCheck.Refused(code, message, details);
                }

                // MOD-0176 F2 (pack §8.3, CT-F2): the expiry hooks' pre-check — FEFO on an issue, minimum remaining shelf life on a goods
                // receipt. A refusal answers 422 here; the posting transaction re-checks the same rule on the committed state (authoritative).
                var expiry = await _expiry.PreCheckAsync(new Expiry.Ports.ExpiryPostingRequest(
                    movement.MovementType, direction.FromStockStatus, direction.ToStockStatus, direction.FromWarehouseId, direction.ToWarehouseId,
                    movement.ItemId, movement.SkuId, movement.SkuLevel, movement.SourceModule, lot, movement.PostingDate, movement.FefoBypassReason,
                    sku.MinRemainingShelfLifeDays), ct);
                if (expiry.Refusal is { } expiryRefusal)
                {
                    return MasterDataCheck.Refused(expiryRefusal.Code, expiryRefusal.Message, expiryRefusal.Details);
                }

                expiryInput = expiry.Input;
            }
            else if (lotControlled && direction.FromLocationId is null)
            {
                // ASSUMPTION-0174-10 (revised, CT K4-4): an inbound movement of a lot-controlled item names a registered lot
                // once the switch is on or the tenant's migration closed typing; the posting transaction re-checks it.
                _typedGuard.RefuseTypedWhenClosed = true;
                if (enforce || await _lotMaster.IsManualLotEntryClosedAsync(ct))
                {
                    return MasterDataCheck.Refused(InventoryErrorCodes.UnknownLot,
                        enforce
                            ? "lotId must name a registered lot for an inbound movement (Trace:EnforceRegisteredLots)."
                            : "lotId must name a registered lot for an inbound movement: typed lot entry is closed for this tenant (migration completed).",
                        new Dictionary<string, object?> { ["lotId"] = lotId });
                }
            }
        }

        if (serialControlled)
        {
            var distinct = movement.SerialIds?.Distinct(StringComparer.Ordinal).Count() ?? 0;
            if (baseQuantity != decimal.Truncate(baseQuantity) || distinct != baseQuantity)
            {
                return MasterDataCheck.Refused(InventoryErrorCodes.SerialCountMismatch,
                    "The item is serial-controlled: the number of distinct serialIds must equal the base quantity.",
                    new Dictionary<string, object?>
                    {
                        ["serialCount"] = distinct.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["baseQuantity"] = InventoryQuantity.ToWire(baseQuantity)
                    });
            }

            // ── SERIALS (WP-0174-P2): one batched registry read for the whole movement ──────────────────────
            var serials = await _lotMaster.ValidateSerialsAsync(new SerialMovement(
                movement.ItemId, movement.SkuId, movement.SkuLevel, lot?.Id, movement.SerialIds!,
                direction.FromWarehouseId, direction.FromLocationId, direction.ToWarehouseId, direction.ToLocationId,
                AcceptUnregistered: !enforce && lot is null), ct);
            if (!serials.IsValid)
            {
                return MasterDataCheck.Refused(serials.Code!, serials.Message!, LotPostingErrors.SerialDetails(serials.Serials));
            }
        }

        decided.Context = new LotPostingContext(
            SerialLifecycle: serialControlled && movement.SerialIds is { Count: > 0 },
            AcceptUnregisteredSerials: !enforce && lot is null,
            RegisterNewSerials: true,
            Expiry: expiryInput);

        // ── LOCATION ──────────────────────────────────────────────────────────────────────────────────────
        var sides = new List<(string WarehouseId, string LocationId)>();
        if (direction.FromLocationId is not null) sides.Add((direction.FromWarehouseId!, direction.FromLocationId));
        if (direction.ToLocationId is not null) sides.Add((direction.ToWarehouseId!, direction.ToLocationId));

        var ids = sides.Select(s => s.LocationId).Distinct(StringComparer.Ordinal).ToArray();
        var existence = await _locationMaster.ValidateLocationsAsync(ids, ct);

        foreach (var (warehouseId, locationId) in sides)
        {
            var refusal = await CheckLocationAsync(existence, warehouseId, locationId, ct);
            if (refusal is not null)
            {
                return refusal;
            }
        }

        return MasterDataCheck.Valid(baseQuantity, uom.BaseUomId);
    }

    private async Task<MasterDataCheck?> CheckLocationAsync(
        IReadOnlyList<LocationValidation> existence, string warehouseId, string locationId, CancellationToken ct)
    {
        var details = new Dictionary<string, object?> { ["locationId"] = locationId, ["warehouseId"] = warehouseId };

        var exists = existence.FirstOrDefault(r => string.Equals(r.LocationId, locationId, StringComparison.Ordinal));
        if (exists is null || !exists.Exists || exists.IsStockable == false)
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.UnknownLocation, "Location does not exist or is not stockable.", details);
        }

        var location = await _locationMaster.GetLocationAsync(locationId, ct);
        if (location is null
            || !string.Equals(location.Status, "Active", StringComparison.Ordinal)
            || !location.IsStockable)
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.UnknownLocation, "Location is not an Active stockable location.", details);
        }

        if (_policy.EnforceLocationLegalEntityMatch && !SameId(location.LegalEntityId, _tenantContext.LegalEntityId))
        {
            // A location of another legal entity is, for this caller, unknown (ASSUMPTION-0173-07).
            return MasterDataCheck.Refused(InventoryErrorCodes.UnknownLocation, "Location does not belong to the caller's legal entity.", details);
        }

        var path = await _locationMaster.GetLocationPathAsync(locationId, ct);
        if (path is null || !path.ContainsWarehouse(warehouseId))
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.UnknownLocation, "Location is not under the named warehouse.", details);
        }

        return null;
    }

    private static bool SameId(string? wire, Guid expected)
        => Guid.TryParse(wire, out var parsed) && parsed == expected;

    /// <summary>What the lot / serial part of the check decided for the posting transaction (set only when it passed).</summary>
    private sealed class LotDecision
    {
        public LotPostingContext? Context { get; set; }
    }
}
