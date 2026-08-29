namespace Diten.CrmService.Application.Features.Segmentation.Contract;

/// <summary>Which list filters the runtime actually supports server-side, so a UI never offers a filter that silently
/// does nothing. There is no recommend / score / next-best parameter anywhere, by construction.</summary>
public sealed record SegmentSupportedFilters(
    IReadOnlyList<string> Segments,
    IReadOnlyList<string> Targets,
    IReadOnlyList<string> Resolve)
{
    public static SegmentSupportedFilters Current => new(
        Segments: new[]
        {
            "segmentType", "segmentStatus", "subjectType", "businessUnitId", "segmentCode", "search",
            "includeArchived"
        },
        Targets: new[] { "membershipMode", "includeArchived" },
        Resolve: new[] { "effectiveAt", "limit", "offset", "includeExcluded" });
}
