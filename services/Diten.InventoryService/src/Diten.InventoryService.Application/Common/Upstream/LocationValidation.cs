namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>One result of LOCATION <c>validateLocations</c>.</summary>
public sealed record LocationValidation(string LocationId, bool Exists, bool? IsStockable);
