namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// MOD-0167 FU02 Phase-2 territory seam: current coverage for the whole candidate set in ONE bulk read.
/// <para>The implementation delegates to the existing MOD-0151 <c>AccountCurrentCoverageResolver</c> exactly as it is:
/// its signature is not widened and no territory aggregate is mutated. When the tenant has no operationally valid
/// model the answer is <see cref="SegmentCoverageLoad.Unavailable"/> — no coverage is fabricated, a candidate carrying
/// a territory criterion is eliminated with <c>territory_coverage_unavailable</c>, and the resolution still COMPLETES.
/// In-service degradation is an answer, not a dependency failure.</para>
/// </summary>
public interface ISegmentTerritoryCoverageReader
{
    Task<SegmentCoverageLoad> LoadAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accountIds,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken);
}
