using System.Text.Json.Serialization;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Models;

public sealed record BusinessReferenceDataVerifiedUom(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("display_text")] string DisplayText,
    [property: JsonPropertyName("sort_order")] int SortOrder,
    [property: JsonPropertyName("maximum_decimal_precision")] int MaximumDecimalPrecision);

public sealed record BusinessReferenceDataVerifiedUomResult(
    [property: JsonPropertyName("uoms")]
    IReadOnlyList<BusinessReferenceDataVerifiedUom> Uoms);
