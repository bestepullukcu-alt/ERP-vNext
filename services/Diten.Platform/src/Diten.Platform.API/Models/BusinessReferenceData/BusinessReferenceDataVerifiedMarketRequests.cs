using System.Text.Json;
using System.Text.Json.Serialization;

namespace Diten.Platform.API.Models.BusinessReferenceData;

public sealed class BusinessReferenceDataVerifiedMarketResolveRequest
{
    [JsonPropertyName("market_code")]
    public string? MarketCode { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalFields { get; init; }
}
