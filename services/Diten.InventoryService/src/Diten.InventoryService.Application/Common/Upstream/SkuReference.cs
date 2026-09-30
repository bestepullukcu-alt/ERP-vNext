namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>The fields of PRODUCT-MASTER <c>SkuRef</c> (getSku) that MOD-0173 needs; <see cref="ItemId"/> is the D-SKU-LINK.</summary>
public sealed record SkuReference(string SkuId, string? SkuLevel, string? ItemId);
