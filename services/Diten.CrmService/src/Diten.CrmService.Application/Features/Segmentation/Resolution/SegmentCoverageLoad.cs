namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// The Phase-2 territory read. <see cref="CoverageAvailable"/> is the honest distinction MOD-0151-FU05A already draws:
/// <b>false</b> means the tenant has no operationally valid territory model at this instant, so coverage cannot be
/// answered at all and a candidate carrying a territory criterion is eliminated with
/// <c>territory_coverage_unavailable</c>; <b>true</b> with an empty row for an account is a genuine negative
/// ("has-coverage = false"), not an outage. A default coverage is never fabricated in either case.
/// </summary>
public sealed record SegmentCoverageLoad(
    bool CoverageAvailable,
    IReadOnlyList<SegmentCoverageProjection> Coverage)
{
    public static readonly SegmentCoverageLoad Unavailable =
        new(false, Array.Empty<SegmentCoverageProjection>());
}
