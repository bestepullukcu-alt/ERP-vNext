using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitContentSequence;

// ---------------------------------------------------------------------------------------------------------------
// MOD-0155 FU04 — Visit Content Sequence. The resolver's I/O contract, in ONE file (the same one-file exception the
// Segmentation / RouteOptimization models use). NOTHING here is a persisted document: the result is a DERIVED,
// TRANSIENT value object (D-NO-AGGREGATE, §4). FU04 opens no collection and TenantId appears in no payload — it is
// server-resolved. Storage of the resolved content position stays FU01's PlannedVisitContentRef; FU04 computes and
// returns, and the FU01 handler writes (2.2 boundary).
// ---------------------------------------------------------------------------------------------------------------

/// <summary>
/// The context a caller supplies to resolve "the next content + its visit duration" for one planned visit to a doctor.
/// <para>It is deliberately FLAT and self-contained so the resolver stays pure over the read seams: the caller (the
/// FU01 default-fill handler, the FU05 packing engine, or the preview endpoint) reads FU01's last visit and passes the
/// prior <see cref="PriorStageIndex"/> — FU04 never opens the PlannedVisit store itself (isolation, §5/§6). Every id is
/// a reference to look up through a seam, never a validated FK.</para>
/// </summary>
public sealed record VisitContentSequenceRequest(
    string SubjectType,
    Guid SubjectId,
    Guid? SegmentId,
    Guid? StrategyTemplateId,
    Guid? CyclePeriodId,
    int? PriorStageIndex,
    DateTimeOffset? EffectiveAt = null);

/// <summary>
/// The DERIVED, never-persisted answer (§4.1): which stage comes next, its promo / non-promo content split, and the
/// visit duration the FU06B calculator yields from those counts. <c>Status</c> and <c>ReasonCodes</c> make every
/// fail-closed outcome a CODED result, never a silent default (§8).
/// </summary>
public sealed record VisitContentSequenceResult(
    string Status,
    Guid? JourneyId,
    Guid? StageId,
    int? StageIndex,
    string? StageCode,
    string? StageDisplayName,
    string ContentSource,
    Guid? StrategyTemplateId,
    int PromoItemCount,
    int NonPromoItemCount,
    int VisitDurationMinutes,
    IReadOnlyList<string> ReasonCodes,
    DateTimeOffset ResolvedAt)
{
    public static VisitContentSequenceResult NotResolved(
        string status, IReadOnlyList<string> reasonCodes, DateTimeOffset at, Guid? journeyId = null,
        Guid? strategyTemplateId = null)
        => new(
            status, journeyId, null, null, null, null,
            PlannedVisitContentSource.Strategy, strategyTemplateId, 0, 0, 0, reasonCodes, at);
}

/// <summary>Resolution outcome vocabulary (in-domain, fail-closed — §3). A <c>no-*</c> / <c>end-of-journey</c> value is
/// a coded answer, never a thrown error: the resolver reports, it is not an engine.</summary>
public static class VisitContentSequenceStatus
{
    public const string Resolved = "resolved";
    public const string NoStrategy = "no-strategy";
    public const string NoJourney = "no-journey";
    public const string EndOfJourney = "end-of-journey";
    public const string NotApplicable = "not-applicable";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Resolved, NoStrategy, NoJourney, EndOfJourney, NotApplicable
    };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);
}

/// <summary>Machine-readable reason codes so a caller (and the smoke script) can branch on the code, not on prose.</summary>
public static class VisitContentSequenceReasonCodes
{
    /// <summary>No active StrategyTemplate resolves for the doctor / segment (V1).</summary>
    public const string StrategyNotFound = "strategy_not_found";

    /// <summary>The bound journey is not published / effective, or has no active stages (V2/V3).</summary>
    public const string JourneyNotPublished = "journey_not_published";

    /// <summary>Next-stage advanced past the last stage — the end-of-journey flag (V4, D-END-OF-JOURNEY = flag).</summary>
    public const string JourneyCompleted = "journey_completed";

    /// <summary>No CycleCapacity is pinned to the cycle period, so no duration can be computed (V5).</summary>
    public const string CapacityNotFound = "capacity_not_found";

    /// <summary>The StrategyTemplate / its promoted product lines could not be resolved, so the promo split is
    /// fail-closed to zero and the duration falls back to ReportDuration only (V6, D-CONTENT-SPLIT §4.5).</summary>
    public const string ContentSplitUnresolved = "content_split_unresolved";

    public static readonly IReadOnlyList<string> All = new[]
    {
        StrategyNotFound, JourneyNotPublished, JourneyCompleted, CapacityNotFound, ContentSplitUnresolved
    };
}
