using System.Text.Json.Serialization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.StockPresence;

/// <summary>
/// GET /api/internal/inventory/stock-presence. Raw values as received; <paramref name="KeyIdentifier"/> is the configured
/// identifier of the service key that ALREADY authenticated (the key filter runs first) — it goes to the audit row only.
/// </summary>
public sealed record GetStockPresenceQuery(
    string KeyIdentifier,
    string? TenantId,
    string? SkuId,
    string? SkuLevel) : IRequest<Response<StockPresenceDto>>;

/// <summary>Contract <c>StockPresence</c> (1.1.0): no quantity, ever.</summary>
public sealed class StockPresenceDto
{
    [JsonPropertyName("skuId")]
    public string SkuId { get; init; } = string.Empty;

    [JsonPropertyName("skuLevel")]
    public string SkuLevel { get; init; } = string.Empty;

    [JsonPropertyName("hasNonZeroStock")]
    public bool HasNonZeroStock { get; init; }

    [JsonPropertyName("coverage")]
    public string Coverage { get; init; } = StockPresenceContract.CoverageComplete;

    [JsonPropertyName("asOf")]
    public string AsOf { get; init; } = string.Empty;

    [JsonPropertyName("contractVersion")]
    public string ContractVersion { get; init; } = InventoryContract.Version;
}
