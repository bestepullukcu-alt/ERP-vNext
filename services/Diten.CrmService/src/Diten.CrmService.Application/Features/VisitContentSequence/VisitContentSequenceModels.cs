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
/// <para><b>WP-SB-3b:</b> the stage now comes from the doctor's <c>JourneyProgress</c> per product, so
/// <see cref="PriorStageIndex"/> is no longer read (kept for the wire shape). <see cref="PendingExposures"/> carries the
/// visits already planned but not yet completed that tell a product before this one — the resolver projects the stage
/// and the exposure that far (DESIGN-SB-3 §3.3, CT projection rule).</para>
/// </summary>
public sealed record VisitContentSequenceRequest(
    string SubjectType,
    Guid SubjectId,
    Guid? SegmentId,
    Guid? StrategyTemplateId,
    Guid? CyclePeriodId,
    int? PriorStageIndex,
    DateTimeOffset? EffectiveAt = null,
    IReadOnlyList<VisitContentPendingExposure>? PendingExposures = null,
    // WP-VP-3C (K-7) — the rep's pick for this doctor (rep-pick source), which visit of the doctor in the period this is
    // (1-based; the rep-pick order shifts by n − 1, K-7e) and the products the previous visit could not hold (they come
    // first among the rep picks, S-2).
    IReadOnlyList<VisitContentProductPick>? RepProducts = null,
    int VisitOrdinal = 1,
    IReadOnlyList<Guid>? CarryOverProductIds = null);

/// <summary>WP-VP-3C — one product the rep picked for the doctor (role promo / non-promo; null = promo, K-7d).</summary>
public sealed record VisitContentProductPick(Guid ProductId, string? ProductCode, string? Role);

/// <summary>WP-VP-3C — a product the visit's role limit left out (<c>max_promo</c> / <c>max_non_promo</c>); it comes first
/// in the doctor's next visit.</summary>
public sealed record VisitContentOverflow(Guid ProductId, string? ProductCode, string Role, string Source, string Reason);

/// <summary>WP-SB-3b — <paramref name="Count"/> earlier, not yet completed visits that tell
/// <paramref name="ProductId"/> on <paramref name="JourneyId"/>.</summary>
public sealed record VisitContentPendingExposure(Guid ProductId, Guid JourneyId, int Count);

/// <summary>
/// The DERIVED, never-persisted answer (§4.1): which stage comes next, its promo / non-promo content split, and the
/// visit duration the FU06B calculator yields from those counts. <c>Status</c> and <c>ReasonCodes</c> make every
/// fail-closed outcome a CODED result, never a silent default (§8).
/// <para><b>WP-SB-3b (v2):</b> <see cref="Items"/> is the visit's product list (≤ MaxPromoProducts promo +
/// ≤ MaxNonPromoProducts non-promo, weighted rotation, each on its own journey stage). The top-level journey / stage
/// fields are filled from the FIRST PROMO item (else the first item) for the existing consumers;
/// <see cref="PromoItemCount"/> / <see cref="NonPromoItemCount"/> are now PRODUCT counts (they were content-item counts)
/// and the duration is computed from them. No item carries a play or campaign id (ARCH GATE S3-9); the top-level
/// <see cref="StrategyTemplateId"/> is unchanged.</para>
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
    DateTimeOffset ResolvedAt,
    IReadOnlyList<VisitContentItem>? Items = null,
    // WP-VP-3C — what the role limits left out of this visit.
    IReadOnlyList<VisitContentOverflow>? OverflowProducts = null)
{
    public static VisitContentSequenceResult NotResolved(
        string status, IReadOnlyList<string> reasonCodes, DateTimeOffset at, Guid? journeyId = null,
        Guid? strategyTemplateId = null)
        => new(
            status, journeyId, null, null, null, null,
            PlannedVisitContentSource.Strategy, strategyTemplateId, 0, 0, 0, reasonCodes, at,
            Array.Empty<VisitContentItem>());
}

/// <summary>WP-SB-3b — one product of the visit: its journey stage, the path version that stage tells (the current
/// release for <c>latest-published</c>, the pinned version otherwise), the path's active steps in order and its claims.
/// <see cref="Warnings"/> never drop the product (e.g. <c>journey_audience_mismatch</c>). No play / campaign id here.</summary>
public sealed record VisitContentItem(
    Guid ProductId,
    string? ProductCode,
    string Role,
    Guid JourneyId,
    string JourneyCode,
    Guid StageId,
    int StageIndex,
    string StageCode,
    string StageName,
    Guid PathId,
    string PathCode,
    string PathVersion,
    IReadOnlyList<VisitContentStep> Steps,
    IReadOnlyList<VisitContentClaim> Claims,
    IReadOnlyList<string> Warnings,
    // WP-VP-3C (K-7) — where the product came from (play · rep-pick · last-visit · portfolio) and its 1-based place in
    // the visit's final list.
    string Source = PlannedVisitContentItemSources.Play,
    int Order = 0,
    // WP-VP-4G (F4-4) — the MDM product name (read-time; the frozen snapshot on an approved visit). Null = show the code.
    string? ProductName = null)
{
    /// <summary>A product told without content (no journey resolves for it): journey / stage / path empty, the reason as
    /// its warning (<c>no_approved_content</c> / <c>ambiguous_journey</c>).</summary>
    public static VisitContentItem WithoutContent(Guid productId, string? productCode, string role, string source, string warning)
        => new(productId, productCode, role, Guid.Empty, string.Empty, Guid.Empty, 0, string.Empty, string.Empty,
            Guid.Empty, string.Empty, string.Empty, Array.Empty<VisitContentStep>(), Array.Empty<VisitContentClaim>(),
            new[] { warning }, source);

    public bool HasContent => JourneyId != Guid.Empty;
}

/// <summary>WP-SB-3b — one active step of the path, in StepOrder.</summary>
public sealed record VisitContentStep(
    Guid StepId, Guid ContentId, string ContentCode, string Title, string Type, int? Minutes);

/// <summary>WP-SB-3b — one claim of the path.</summary>
public sealed record VisitContentClaim(Guid ClaimId, string ClaimCode);

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

    /// <summary>Next-stage advanced past the last stage — the end-of-journey flag (V4, D-END-OF-JOURNEY = flag).
    /// <b>No longer produced (WP-SB-3b, S3-7):</b> a journey wraps back to its first stage; kept for the vocabulary.</summary>
    public const string JourneyCompleted = "journey_completed";

    /// <summary>No CycleCapacity is pinned to the cycle period, so no duration can be computed (V5).</summary>
    public const string CapacityNotFound = "capacity_not_found";

    /// <summary>The StrategyTemplate / its promoted product lines could not be resolved, so the promo split is
    /// fail-closed to zero and the duration falls back to ReportDuration only (V6, D-CONTENT-SPLIT §4.5).</summary>
    public const string ContentSplitUnresolved = "content_split_unresolved";

    /// <summary>WP-SB-3b — a strategy line without a journey (written before SB-3a): the product is dropped.</summary>
    public const string ProductHasNoJourney = "product_has_no_journey";

    /// <summary>WP-SB-3b — the line's journey is not published / effective or has no active stage: the product is dropped.</summary>
    public const string JourneyUnpublished = "journey_unpublished";

    /// <summary>WP-SB-3b — the stage's path has no current release (latest-published) or its pinned version is no longer
    /// released: the product is dropped (no silent version drift).</summary>
    public const string StagePathUnpublished = "stage_path_unpublished";

    /// <summary>WP-SB-3b — the stored stage index no longer fits the journey (it lost stages): read as stage 0.</summary>
    public const string StageIndexReset = "stage_index_reset";

    /// <summary>WP-SB-3b — WARNING only: the journey's audience profile does not cover the doctor's specialty.</summary>
    public const string JourneyAudienceMismatch = "journey_audience_mismatch";

    /// <summary>WP-VP-3C (K-7a) — the visit has no product at all (no play line, no rep pick): only the report time counts.</summary>
    public const string NoProducts = "no_products";

    /// <summary>WP-VP-3C (K-7f) — a picked product with no published journey telling it: planned without content.</summary>
    public const string NoApprovedContent = "no_approved_content";

    /// <summary>WP-VP-3C (K-7f) — a picked product told by more than one published journey: planned without content.</summary>
    public const string AmbiguousJourney = "ambiguous_journey";

    /// <summary>WP-VP-3C (K-7e) — overflow reasons: the role's per-visit limit was reached.</summary>
    public const string MaxPromo = "max_promo";
    public const string MaxNonPromo = "max_non_promo";

    public static readonly IReadOnlyList<string> All = new[]
    {
        StrategyNotFound, JourneyNotPublished, JourneyCompleted, CapacityNotFound, ContentSplitUnresolved,
        ProductHasNoJourney, JourneyUnpublished, StagePathUnpublished, StageIndexReset, JourneyAudienceMismatch,
        NoProducts, NoApprovedContent, AmbiguousJourney, MaxPromo, MaxNonPromo
    };
}
