using System.Text.Json.Serialization;

namespace Diten.InventoryService.Application.Common.Models;

/// <summary>
/// The INVENTORY-BUNDLE v1 <c>Error</c> schema: <c>{ error: { code, message, details, correlationId }, contractVersion }</c>.
/// <see cref="ContractErrorDetail.Code"/> is omitted (not invented) for refusals that have no declared code, e.g.
/// the framework's 401 / permission 403 (pack ASSUMPTION-0173-04 limits new codes to its own list).
/// </summary>
public sealed class ContractError
{
    [JsonPropertyName("error")]
    public ContractErrorDetail Error { get; init; } = new();

    [JsonPropertyName("contractVersion")]
    public string ContractVersion { get; init; } = InventoryContract.Version;

    public static ContractError Create(
        string? code,
        string message,
        Guid correlationId,
        IReadOnlyDictionary<string, object?>? details = null) => new()
    {
        Error = new ContractErrorDetail
        {
            Code = code,
            Message = message,
            Details = details,
            CorrelationId = correlationId.ToString()
        }
    };
}
