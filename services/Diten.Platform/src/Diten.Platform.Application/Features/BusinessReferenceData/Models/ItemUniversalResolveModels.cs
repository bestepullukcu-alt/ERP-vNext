using System.Text.Json.Serialization;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Models;

// WP-PLT-ITEM-LISTS-01: one value of the item universal catalog (ITEM-UNIVERSAL-Vn). DisplayName is the English technical label (FU01 §4); tenant
// screen labels in seven languages are MOD-0290's (S5).
public sealed record ItemUniversalValue(
    string SetCode,
    string ValueCode,
    string DisplayName,
    int SortOrder,
    IReadOnlyDictionary<string, string?> Attributes);

public sealed record ItemUniversalResolveSelectionInput(string SetCode, string ValueCode, string ResolutionMode);

public sealed record ItemUniversalResolveSelection(
    [property: JsonPropertyName("set_code")] string SetCode,
    [property: JsonPropertyName("value_code")] string ValueCode,
    [property: JsonPropertyName("catalog_version")] string CatalogVersion,
    [property: JsonPropertyName("catalog_version_id")] Guid CatalogVersionId,
    [property: JsonPropertyName("catalog_version_number")] int CatalogVersionNumber,
    [property: JsonPropertyName("resolution_mode")] string ResolutionMode,
    [property: JsonPropertyName("resolved_at_utc")] DateTimeOffset ResolvedAtUtc,
    [property: JsonPropertyName("is_retired")] bool IsRetired,
    [property: JsonPropertyName("selectable_for_new")] bool SelectableForNew,
    [property: JsonPropertyName("attributes")] IReadOnlyDictionary<string, string?> Attributes);

public sealed record ItemUniversalNotFound(
    [property: JsonPropertyName("set_code")] string SetCode,
    [property: JsonPropertyName("value_code")] string ValueCode);

public sealed record ItemUniversalResolveResult(
    [property: JsonPropertyName("selections")] IReadOnlyList<ItemUniversalResolveSelection> Selections,
    [property: JsonPropertyName("not_found")] IReadOnlyList<ItemUniversalNotFound> NotFound);
