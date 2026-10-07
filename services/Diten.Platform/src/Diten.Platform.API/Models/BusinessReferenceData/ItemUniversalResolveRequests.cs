using System.Text.Json;
using System.Text.Json.Serialization;

namespace Diten.Platform.API.Models.BusinessReferenceData;

// WP-PLT-ITEM-LISTS-01: the item-universal resolve body. Unknown fields are refused (400), never ignored.
public sealed class ItemUniversalResolveRequest
{
    [JsonPropertyName("selections")]
    public List<ItemUniversalResolveSelectionRequest?>? Selections { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalFields { get; init; }
}

public sealed class ItemUniversalResolveSelectionRequest
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
