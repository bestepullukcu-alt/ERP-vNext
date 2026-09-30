namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>One result of PRODUCT-MASTER <c>validateRefs</c>. Ids are kept as sent back (strings) and matched by value.</summary>
public sealed record ProductRefValidation(string? ItemId, string? SkuId, string? SkuLevel, bool Exists);
