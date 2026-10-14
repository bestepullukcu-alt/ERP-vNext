using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.ValueObjects;

/// <summary>
/// The reservation scope inside a Tenant + LE (pack ASSUMPTION-0173-12): item + sku + sku level. Reservations and the
/// <c>inventory_sku_positions</c> projection are keyed by it; bin / lot allocation belongs to MOD-0176 / MOD-0178.
/// </summary>
public sealed record SkuKey(Guid ItemId, Guid SkuId, SkuLevel SkuLevel);
