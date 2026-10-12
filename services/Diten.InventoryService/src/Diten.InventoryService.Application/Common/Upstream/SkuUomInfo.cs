using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>PRODUCT-MASTER <c>UomInfo</c> (getSkuUom).</summary>
public sealed record SkuUomInfo(string BaseUomId, IReadOnlyList<UomConversionRule> Conversions);
