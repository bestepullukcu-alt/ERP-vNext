using System.Text.Json.Serialization;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Models;

// MOD-0290-FU04 S5b (BL-600): every value of the asked ITEM-UNIVERSAL sets, for MDM's item / stock forms to offer as a
// choice. A list to choose from, never the proof: MDM still resolves every chosen code exactly (resolve).
public sealed record ItemUniversalEnumeratedValue(
    [property: JsonPropertyName("value_code")] string ValueCode,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("sort_order")] int SortOrder,
    [property: JsonPropertyName("attributes")] IReadOnlyDictionary<string, string?> Attributes);

public sealed record ItemUniversalEnumeratedSet(
    [property: JsonPropertyName("set_code")] string SetCode,
    [property: JsonPropertyName("catalog_version")] string CatalogVersion,
    [property: JsonPropertyName("catalog_version_id")] Guid CatalogVersionId,
    [property: JsonPropertyName("catalog_version_number")] int CatalogVersionNumber,
    [property: JsonPropertyName("values")] IReadOnlyList<ItemUniversalEnumeratedValue> Values);

public sealed record ItemUniversalEnumerationResult(
    [property: JsonPropertyName("sets")] IReadOnlyList<ItemUniversalEnumeratedSet> Sets);
