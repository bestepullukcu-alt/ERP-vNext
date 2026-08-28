namespace Diten.CrmService.Application.Features.Campaign;

/// <summary>MOD-0165 FU04 read model for a campaign. TenantId is never echoed — it is server-resolved from the JWT
/// claim and never accepted from a payload. Every Brand/Product/Subject/Concept/Journey/Path/Content member is an ID
/// reference; no master field is projected, because a copied name goes stale.</summary>
public sealed record CampaignDto(
    Guid CampaignId,
    string CampaignCode,
    string CampaignName,
    string CampaignType,
    string CampaignStatus,
    string? ObjectiveType,
    string? BusinessUnitId,
    // FU09 - the campaign's discriminated address. ScopeType is the EFFECTIVE one, so a pre-FU09 row reads as the
    // scope it always had rather than as an empty string; ScopeRef is the derived second half of the address.
    string ScopeType,
    string? ScopeRef,
    string? CountryScope,
    Guid? LegalEntityId,
    string? DefaultConsentChannel,
    string? DefaultConsentPurpose,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    Guid? CyclePeriodId,
    CampaignCyclePeriodDto? CyclePeriod,
    // FU10 - the EFFECTIVE mode, so a pre-FU10 row reads as `manual` rather than as an empty string.
    string TargetingMode,
    // The pinned segment ids, and a read-time projection of what they currently are. Both are always returned: the
    // ids are the campaign's own data, the projection is a label that may legitimately be missing.
    IReadOnlyList<Guid> TargetedSegmentIds,
    IReadOnlyList<CampaignTargetedSegmentDto> TargetedSegments,
    string? Description,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

public sealed record CampaignListDto(IReadOnlyList<CampaignDto> Items, int Total);

/// <summary>
/// MOD-0165 FU10 — a READ-TIME projection of one targeted segment, so a screen can show "GP-ISTANBUL" instead of a
/// bare GUID.
/// <para><b>Never persisted.</b> The campaign stores the id and the link time; everything else here is rebuilt on
/// every read, because a copied code or name goes stale the moment the segment is renamed.</para>
/// <para><see cref="Superseded"/> is surfaced rather than acted on: a newer segment version does not change what the
/// campaign targets, and moving to it is the author's deliberate decision.</para>
/// <para><see cref="IsResolvable"/> is false when the segment could not be read at all — the campaign still shows the
/// id it pinned instead of inventing a label.</para>
/// </summary>
public sealed record CampaignTargetedSegmentDto(
    Guid SegmentId,
    DateTimeOffset LinkedAt,
    bool IsResolvable,
    string? SegmentCode,
    string? SegmentName,
    string? SubjectType,
    string? SegmentStatus,
    bool Superseded,
    int? SegmentVersion);

/// <summary>MOD-0165 FU09 - one selectable value for a scope level.</summary>
public sealed record CampaignScopeOptionDto(string Value, string Label);

/// <summary>
/// MOD-0165 FU09 - one round trip for the cascading scope selector: the levels, the governed country values, the
/// tenant's referenceable legal entities, and the business units its territory plans cover.
/// <para><b>Three sources, three separate readiness flags.</b> An empty list because a reference set is unpublished,
/// an empty list because MDM is unreachable, and an empty list because no plan matches are three different
/// situations. A UI that cannot tell them apart shows the author a silent empty dropdown and no way to act, so each is
/// reported on its own. A hardcoded fallback list is forbidden in all three cases.</para>
/// <para><c>BusinessUnitFromTerritory</c> says whether the business-unit list is the Territory-derived narrowing or
/// the full published vocabulary it falls back to when no plan matches - which keeps business-unit campaigns
/// authorable before their field plan exists.</para>
/// </summary>
public sealed record CampaignScopeOptionsDto(
    IReadOnlyList<string> ScopeTypes,
    IReadOnlyList<CampaignScopeOptionDto> Countries,
    bool CountrySetPublished,
    IReadOnlyList<CampaignScopeOptionDto> LegalEntities,
    bool LegalEntityLookupAvailable,
    IReadOnlyList<CampaignScopeOptionDto> BusinessUnits,
    bool BusinessUnitSetPublished,
    bool BusinessUnitFromTerritory);

/// <summary>
/// MOD-0165 FU09 - the cycle periods a campaign at a given scope may bind to, most specific address first.
/// <para>The applicability rule lives on the SERVER. A picker that decided it in the browser would be a second copy of
/// the rule, and a direct API call would walk straight past it.</para>
/// </summary>
public sealed record CampaignApplicableCyclePeriodsDto(
    string ScopeType,
    string? ScopeRef,
    IReadOnlyList<string> ApplicableScopes,
    IReadOnlyList<CampaignCyclePeriodDto> Items);

/// <summary>
/// MOD-0165 FU08 — a READ-TIME projection of the bound cycle period, so a grid or a detail page can show
/// "2026 / 3" instead of a bare GUID.
/// <para><b>It is never persisted.</b> Nothing here is written onto the campaign document: the campaign stores the id
/// alone and this projection is rebuilt on every read, because a copied code or window goes stale the moment the
/// period is renamed or re-dated.</para>
/// <para><c>null</c> when the campaign is unbound, and also when the referenced period could not be read — a display
/// projection never invents a label, and the write path is where a dangling reference is refused.</para>
/// </summary>
public sealed record CampaignCyclePeriodDto(
    Guid CyclePeriodId,
    string CycleCode,
    string CycleName,
    int Year,
    int SequenceInYear,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string CycleStatus);

/// <summary>MOD-0165 FU04 read model for a campaign target.</summary>
public sealed record CampaignTargetDto(
    Guid CampaignTargetId,
    Guid CampaignId,
    string TargetType,
    Guid TargetId,
    string? TargetDisplayName,
    string TargetStatus,
    string TargetSource,
    string? SourceReferenceType,
    Guid? SourceReferenceId,
    Guid? SnapshotBatchId,
    string SelectionReason,
    IReadOnlyList<string> ReasonCodes,
    // MOD-0165 FU11 - Priority is the DEPRECATED integer, kept so an existing consumer keeps reading what it read
    // yesterday. PriorityLevel is what to display: the stated band, or the band derived from that integer for rows
    // written before FU11.
    int? Priority,
    string? PriorityLevel,
    CampaignTargetConsentEvaluationDto? ConsentEvaluation,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? ExclusionReason,
    string? Notes,
    IReadOnlyList<CampaignExternalReferenceDto> ExternalReferences,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

public sealed record CampaignTargetListDto(IReadOnlyList<CampaignTargetDto> Items, int Total);

/// <summary>
/// Consent DECISION provenance as echoed back. Deliberately carries no <c>consentStatus</c>,
/// no <c>preferenceStatus</c> and no consent/preference record payload — only the MOD-0164 verdict, the ids it
/// matched, the question it answered and the evaluator version.
/// </summary>
public sealed record CampaignTargetConsentEvaluationDto(
    string Decision,
    string EligibilityStatus,
    IReadOnlyList<string> ReasonCodes,
    DateTimeOffset EvaluatedAt,
    Guid? MatchedConsentId,
    IReadOnlyList<Guid> MatchedPreferenceIds,
    string EvaluatorVersion,
    string SelectionReason,
    string? Channel,
    string? Purpose,
    bool FilterApplied);

/// <summary>External/legacy identity as echoed back (same six-field contract as MOD-0290-FU01 / MOD-0164-FU02).</summary>
public sealed record CampaignExternalReferenceDto(
    string SourceSystem,
    string ExternalId,
    string? ExternalCode,
    string? ExternalName,
    DateTimeOffset? ImportedAt,
    bool IsPrimary);

/// <summary>Inbound external-reference line shared by the campaign and target write commands.</summary>
public sealed record CampaignExternalReferenceInput(
    string SourceSystem,
    string ExternalId,
    string? ExternalCode = null,
    string? ExternalName = null,
    DateTimeOffset? ImportedAt = null,
    bool IsPrimary = false);

/// <summary>One inbound row of a static snapshot. The caller supplies the identity; FU04 resolves no membership.</summary>
public sealed record CampaignSnapshotTargetItem(
    string TargetType,
    Guid TargetId,
    string? TargetDisplayName = null,
    // MOD-0165 FU11 - a snapshot caller may send either. The integer stays accepted exactly as before so no existing
    // caller has to change; PriorityLevel is the band FU11 introduced. Both are stored as given and the band wins on
    // read - the snapshot decides nothing new here.
    int? Priority = null,
    string? PriorityLevel = null,
    string? SourceReferenceType = null,
    Guid? SourceReferenceId = null);

/// <summary>
/// Outcome of one snapshot run. Additive by construction: <see cref="ArchivedCount"/> does not exist because a
/// snapshot never closes an earlier target. Every row is accounted for in exactly one bucket, and every bucket entry
/// carries its reason.
/// </summary>
public sealed record CampaignTargetSnapshotResultDto(
    Guid SnapshotBatchId,
    Guid CampaignId,
    string SourceType,
    string? SourceReferenceType,
    Guid? SourceReferenceId,
    DateTimeOffset EffectiveAt,
    bool ConsentFilterApplied,
    string? ConsentChannel,
    string? ConsentPurpose,
    int RequestedCount,
    int CreatedCount,
    int ReconciledCount,
    int ActiveCount,
    int ExcludedCount,
    int ConflictCount,
    IReadOnlyList<CampaignSnapshotRowResultDto> Rows,
    IReadOnlyList<string> ReasonCodes,
    string SelectionReason);

/// <summary>Per-row snapshot outcome — what happened to this target and why.</summary>
public sealed record CampaignSnapshotRowResultDto(
    string TargetType,
    Guid TargetId,
    Guid? CampaignTargetId,
    string Outcome,
    string TargetStatus,
    string? ExclusionReason,
    IReadOnlyList<string> ReasonCodes,
    CampaignTargetConsentEvaluationDto? ConsentEvaluation,
    string Message);

/// <summary>Per-row snapshot outcome vocabulary.</summary>
public static class CampaignSnapshotRowOutcome
{
    /// <summary>A new campaign target was inserted.</summary>
    public const string Created = "created";

    /// <summary>An existing target from the same source was updated in place — no duplicate was produced.</summary>
    public const string Reconciled = "reconciled";

    /// <summary>An existing target is owned by a DIFFERENT source; nothing was written for this row (409-class).</summary>
    public const string SourceConflict = "source_conflict";

    /// <summary>The row was rejected before any write (structural problem in the row itself).</summary>
    public const string Rejected = "rejected";

    public static readonly IReadOnlyList<string> All = new[] { Created, Reconciled, SourceConflict, Rejected };
}
