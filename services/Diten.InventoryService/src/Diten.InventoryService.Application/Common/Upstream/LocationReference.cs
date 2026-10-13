namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>The fields of LOCATION <c>LocationRef</c> (getLocation) that MOD-0173 needs.</summary>
public sealed record LocationReference(
    string LocationId,
    string? Type,
    string? LegalEntityId,
    string? Status,
    bool IsStockable);
