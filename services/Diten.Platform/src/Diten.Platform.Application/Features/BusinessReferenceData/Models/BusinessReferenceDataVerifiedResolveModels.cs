using System.Text.Json.Serialization;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Models;

public sealed record BusinessReferenceDataVerifiedResolveSelectionInput(
    string SetCode,
    string ValueCode,
    string ResolutionMode);

public sealed record BusinessReferenceDataVerifiedResolveSelection(
    [property: JsonPropertyName("set_code")] string SetCode,
    [property: JsonPropertyName("value_code")] string ValueCode,
    [property: JsonPropertyName("catalog_version_id")] Guid CatalogVersionId,
    [property: JsonPropertyName("catalog_version_number")] int CatalogVersionNumber,
    [property: JsonPropertyName("resolution_mode")] string ResolutionMode,
    [property: JsonPropertyName("resolved_at_utc")] DateTimeOffset ResolvedAtUtc,
    [property: JsonPropertyName("is_retired")] bool IsRetired,
    [property: JsonPropertyName("selectable_for_new")] bool SelectableForNew);

public sealed record BusinessReferenceDataVerifiedResolveResult(
    [property: JsonPropertyName("selections")]
    IReadOnlyList<BusinessReferenceDataVerifiedResolveSelection> Selections);
