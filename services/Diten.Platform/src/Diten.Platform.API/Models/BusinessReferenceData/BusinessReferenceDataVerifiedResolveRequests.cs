using System.Text.Json;
using System.Text.Json.Serialization;

namespace Diten.Platform.API.Models.BusinessReferenceData;

public sealed class BusinessReferenceDataVerifiedResolveRequest
{
    [JsonPropertyName("selections")]
    public List<BusinessReferenceDataVerifiedResolveSelectionRequest>? Selections { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalFields { get; init; }
}

public sealed class BusinessReferenceDataVerifiedResolveSelectionRequest
{
    [JsonPropertyName("set_code")]
    public string? SetCode { get; init; }

    [JsonPropertyName("value_code")]
    public string? ValueCode { get; init; }

    [JsonPropertyName("resolution_mode")]
    public string? ResolutionMode { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalFields { get; init; }
}
