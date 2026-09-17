namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy;

/// <summary>MOD-0165 FU03 read model for a single policy (list + detail). Mirrors the aggregate; TenantId is never
/// echoed as it is server-resolved.</summary>
public sealed record VisitFrequencyPolicyDto(
    Guid PolicyId,
    string PolicyCode,
    string PolicyName,
    string? Description,
    string TargetType,
    Guid TargetId,
    string? BusinessUnit,
    Guid? TerritoryNodeId,
    Guid? CampaignId,
    Guid? SegmentId,
    Guid? BrandId,
    Guid? ProductId,
    Guid? CycleId,
    Guid? CyclePeriodId,
    string FrequencyType,
    int RequiredVisitCount,
    string PeriodType,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    int Priority,
    string Source,
    string Status,
    string? Notes,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy);

/// <summary>Paged list envelope.</summary>
public sealed record VisitFrequencyPolicyListDto(
    IReadOnlyList<VisitFrequencyPolicyDto> Items,
    int Total);

/// <summary>
/// WP-FREQ-DET-A — read-only DETAY ANALİZ for one policy. Two independent read projections, neither of which mutates
/// anything: <see cref="Impact"/> ("how many entities does this target reach, and how many visits does that imply per
/// quarter?") and <see cref="Conflicts"/> ("which active policies compete for this same target/context, and which one
/// wins?"). The conflicts block is produced by REUSING the FU03 resolve engine (no new resolution logic).
/// </summary>
public sealed record VisitFrequencyPolicyAnalysisDto(
    Guid PolicyId,
    VisitFrequencyPolicyImpactDto Impact,
    VisitFrequencyPolicyConflictsDto Conflicts,
    IReadOnlyList<VisitFrequencyPolicyTimelineEntryDto> Timeline);

/// <summary>
/// WP-FREQ-DET-C — one DURUM AKIŞI row. Real entries come from the policy's embedded audit trail
/// (<see cref="Diten.CrmService.Domain.Entities.VisitFrequencyPolicy.Events"/>) in chronological order; for a policy
/// that predates the trail the timeline is BACKFILLED from the CreatedAt / ArchivedAt timestamps (created + archived
/// only — a weight change leaves no timestamp, so it is never fabricated). The final <see cref="IsFuture"/> entry is the
/// derived "Sonraki değerlendirme" (next-eval): the cycle-period end when the policy is cycle-scoped, else EffectiveTo,
/// else omitted. <see cref="FromValue"/>/<see cref="ToValue"/> carry band codes for a <c>weight-changed</c> entry (or a
/// status pair) so the UI localizes them; they are null for point events.
/// </summary>
public sealed record VisitFrequencyPolicyTimelineEntryDto(
    string Type,
    DateTimeOffset At,
    string? By,
    string? FromValue,
    string? ToValue,
    bool IsFuture);

/// <summary>
/// ETKİ — the reach of a policy's target and the implied quarterly visit load.
/// <para><see cref="TargetCount"/> is the number of entities the target resolves to (segment members, campaign-target
/// snapshot rows, territory-node coverage, or 1 for a single account/contact/link). It is <c>null</c> whenever
/// <see cref="TargetCountComputable"/> is false — never a fabricated 0. A count is uncomputable when the segment
/// candidate cap is exceeded, the segment is missing, or the target type has no ready counting path yet
/// (audience-profile / concept-node); the reason is stated in <see cref="ProjectionNote"/>.</para>
/// <para><see cref="PlannedVisitsPerQuarter"/> = <see cref="TargetCount"/> × RequiredVisitCount normalised to one
/// quarter (month ×3, week ×13, quarter ×1, day ×~91). A cycle / campaign-period / custom period cannot be normalised,
/// so the value is the raw <c>count × RequiredVisitCount</c> and <see cref="ProjectionNote"/> says so. When the count
/// is not computable the projection is <c>null</c> too.</para>
/// </summary>
public sealed record VisitFrequencyPolicyImpactDto(
    int? TargetCount,
    bool TargetCountComputable,
    int? PlannedVisitsPerQuarter,
    string? ProjectionNote);

/// <summary>
/// ÇAKIŞAN — the outcome of running the FU03 resolve engine for the policy's OWN target + context. <see cref="Verdict"/>
/// is the resolve <c>FrequencyStatus</c> (resolved / conflict / unknown), <see cref="SelectedPolicyId"/> is the winner
/// (null when unknown) and <see cref="Candidates"/> lists every considered policy with the reason it did or did not win.
/// </summary>
public sealed record VisitFrequencyPolicyConflictsDto(
    Guid? SelectedPolicyId,
    string Verdict,
    IReadOnlyList<VisitFrequencyPolicyConflictCandidateDto> Candidates);

/// <summary>One competing policy in the conflict analysis: identity, a compact frequency summary
/// (<c>RequiredVisitCount×/PeriodType</c>), its priority, whether it was selected, and the resolve reason code.</summary>
public sealed record VisitFrequencyPolicyConflictCandidateDto(
    Guid PolicyId,
    string PolicyCode,
    string PolicyName,
    string FrequencySummary,
    int Priority,
    bool Selected,
    string Reason);
