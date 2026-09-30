namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>
/// Outcome of the PRODUCT-MASTER + LOCATION checks: either the server-computed base quantity, or a 422 business
/// refusal (<see cref="ErrorCode"/>, <see cref="Message"/>, optional <see cref="Details"/>).
/// </summary>
public sealed record MasterDataCheck(
    decimal BaseQuantity,
    string BaseUomId,
    string? ErrorCode = null,
    string? Message = null,
    IReadOnlyDictionary<string, object?>? Details = null)
{
    public bool IsValid => ErrorCode is null;

    public static MasterDataCheck Valid(decimal baseQuantity, string baseUomId) => new(baseQuantity, baseUomId);

    public static MasterDataCheck Refused(string code, string message, IReadOnlyDictionary<string, object?>? details = null)
        => new(0m, string.Empty, code, message, details);
}
