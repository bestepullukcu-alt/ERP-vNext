using System.Text.Json.Serialization;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Models;

public static class VerifiedMarketCatalogContract
{
    public const string SetCode = "market";
    public const string ResolutionMode = "LATEST";
    public const int MaximumActiveMarketCount = 300;

    public static bool IsCanonicalCode(string? value) =>
        value is { Length: 2 }
        && value[0] is >= 'A' and <= 'Z'
        && value[1] is >= 'A' and <= 'Z';
}

public sealed record BusinessReferenceDataVerifiedMarketSelection(
    [property: JsonPropertyName("set_code")] string SetCode,
    [property: JsonPropertyName("value_code")] string ValueCode,
    [property: JsonPropertyName("catalog_version_id")] Guid CatalogVersionId,
    [property: JsonPropertyName("catalog_version_number")] int CatalogVersionNumber,
    [property: JsonPropertyName("resolution_mode")] string ResolutionMode,
    [property: JsonPropertyName("resolved_at_utc")] DateTimeOffset ResolvedAtUtc);

public sealed record BusinessReferenceDataVerifiedMarketOption(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("display_text")] string DisplayText,
    [property: JsonPropertyName("sort_order")] int SortOrder);

public sealed record BusinessReferenceDataVerifiedMarketResolveResult(
    [property: JsonPropertyName("market")] BusinessReferenceDataVerifiedMarketSelection Market);

public sealed record BusinessReferenceDataVerifiedMarketsResult(
    [property: JsonPropertyName("markets")] IReadOnlyList<BusinessReferenceDataVerifiedMarketOption> Markets);
