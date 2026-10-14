namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>One result of PRODUCT-MASTER <c>validateRefs</c>. Ids are kept as sent back (strings) and matched by value.</summary>
/// <remarks>WP-0173-PM1: v1.1 rows also carry <c>lifecycleStatus</c> and <c>itemKind</c> (X-01; absent on a row that was not found).</remarks>
public sealed record ProductRefValidation(string? ItemId, string? SkuId, string? SkuLevel, bool Exists, string? LifecycleStatus = null, string? ItemKind = null);
