namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>One level of LOCATION <c>getLocationPath</c> (Site → Warehouse → StorageLocation → Zone → Bin).</summary>
public sealed record LocationPathEntry(string? Type, string? Id, string? Code);
