using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>One entry of PRODUCT-MASTER <c>validateRefs</c> (either an item or a sku + level).</summary>
public sealed record ProductRefQuery(Guid? ItemId, Guid? SkuId, SkuLevel? SkuLevel);
