namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>A request-shape error: contract <c>error.code</c> + English message.</summary>
public sealed record FieldError(string Code, string Message);
