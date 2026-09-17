namespace Diten.CrmService.Application.Features.Segmentation;

// ---------------------------------------------------------------------------------------------------------------
// MOD-0167 FU02 — every DTO / read model of the Segmentation feature, in ONE file (the single documented exception to
// the one-public-type-per-file convention). TenantId appears in NO payload: it is server-resolved from the claim.
// No DTO here carries MemberIds, MemberCount, LastResolvedAt or any other piece of runtime state — a segment is a
// definition, and a resolution is a report that persists nothing.
// ---------------------------------------------------------------------------------------------------------------

/// <summary>One row of the segment grid. The criteria tree is projected OUT (only a node counter is exposed) so the
/// list stays cheap; the detail endpoint returns the tree.</summary>
public sealed record SegmentListItemDto(
    Guid SegmentId,
    string SegmentCode,
    string SegmentName,
    string SegmentType,
    string SubjectType,
    string SegmentStatus,
    int SegmentVersion,
    Guid VersionLineageId,
    bool Superseded,
    Guid? SupersededBySegmentId,
    string? BusinessUnitId,
    string? Description,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string MatchMode,
    int CriteriaNodeCount,
    int PredicateCount,
    bool IsCriteriaFrozen,
    DateTimeOffset? CriteriaFrozenAt,
    DateTimeOffset? ActivatedAt,
    bool IsArchived,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Segment detail, including the embedded criteria tree.</summary>
public sealed record SegmentDetailDto(
    Guid SegmentId,
    string SegmentCode,
    string SegmentName,
    string SegmentType,
    string SubjectType,
    string SegmentStatus,
    int SegmentVersion,
    Guid VersionLineageId,
    bool Superseded,
    Guid? SupersededBySegmentId,
    string? BusinessUnitId,
    string? Description,
    string? Notes,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string MatchMode,
    IReadOnlyList<SegmentCriteriaNodeDto> Criteria,
    bool IsCriteriaFrozen,
    DateTimeOffset? CriteriaFrozenAt,
    DateTimeOffset? ActivatedAt,
    string? ActivatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived,
    int Version,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    // WP-SEG-DETAILS6 — ADDITIVE, nullable display names for the timeline. The raw *By ids (the user's `sub`) stay
    // exactly as they were; these are the resolved labels the reader fills in via ONE bulk AuthService call, and each
    // is null when the id could not be resolved (fail-closed) so the UI shows a date without a fabricated name.
    // Appended LAST with null defaults so the mapper's positional projection is unchanged.
    string? CreatedByName = null,
    string? ActivatedByName = null,
    string? UpdatedByName = null);

/// <summary>One node of the embedded criteria tree as it is read back. Flat list plus ParentNodeId (D2).</summary>
public sealed record SegmentCriteriaNodeDto(
    Guid NodeId,
    Guid? ParentNodeId,
    string NodeKind,
    string? GroupOperator,
    string? AttributeCode,
    string? Operator,
    IReadOnlyList<string> Values,
    string? ValueType,
    IReadOnlyDictionary<string, string> Parameters,
    bool Negate,
    int SortOrder,
    string? Label);

/// <summary>One node of the criteria tree as it is WRITTEN. NodeId is optional on input: a create supplies none and
/// the runtime assigns them, so the caller can never forge or reuse an id from another segment.</summary>
public sealed record SegmentCriteriaNodeInput(
    Guid? NodeId,
    Guid? ParentNodeId,
    string NodeKind,
    string? GroupOperator,
    string? AttributeCode,
    string? Operator,
    IReadOnlyList<string>? Values,
    string? ValueType,
    IReadOnlyDictionary<string, string>? Parameters,
    bool Negate,
    int SortOrder,
    string? Label);

/// <summary>A manual membership row (include / exclude). Never a derived member.</summary>
public sealed record TargetCustomerDto(
    Guid TargetCustomerId,
    Guid SegmentId,
    string SubjectType,
    Guid SubjectId,
    string MembershipMode,
    string? SubjectDisplayName,
    string SelectionReason,
    IReadOnlyList<string> ReasonCodes,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Notes,
    bool IsArchived,
    DateTimeOffset? ArchivedAt,
    int Version,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);

/// <summary>How a subject ended up in (or out of) the result. Makes "rule or human?" answerable without reading rows.</summary>
public static class SegmentMembershipSources
{
    public const string Criteria = "criteria";
    public const string ManualInclude = "manual-include";
    public const string ManualExclude = "manual-exclude";
    public const string StaticList = "static-list";

    public static readonly IReadOnlyList<string> All = new[] { Criteria, ManualInclude, ManualExclude, StaticList };
}

/// <summary>One resolved subject. Both accepted and eliminated candidates use this shape, so an elimination is never
/// less visible than an acceptance.
/// <para><see cref="SubjectDisplayName"/> is a display label carried on the candidate projection (no extra read). It
/// is never a source of truth and no rule is evaluated against it — a consumer that needs the real name resolves it
/// from the owning master, exactly as <c>TargetCustomer.SubjectDisplayName</c> already documents.</para>
/// <para><see cref="SubjectSecondaryLabel"/> (WP-SEG-D) is an ADDITIVE, nullable second display line derived from the
/// SAME candidate projection (contact → specialty, account → type, optionally " · city") at no extra read. It is a
/// display label only; it is null for a manual/static member that never passed through the candidate projection. It is
/// appended LAST so the existing positional fields, their order and their meaning are unchanged.</para></summary>
public sealed record SegmentMemberDto(
    Guid SubjectId,
    string SubjectType,
    string? SubjectDisplayName,
    string Verdict,
    string MembershipSource,
    IReadOnlyList<string> ReasonCodes,
    string? SubjectSecondaryLabel = null);

/// <summary>
/// The output of <c>resolve</c>. Deterministic for an unchanged source data set: same member set, same order
/// (SubjectId ASC — never a DateTimeOffset key), same reason codes. NOTHING is persisted to produce it.
/// <see cref="Excluded"/> is only populated when the caller asks for it, but the COUNTS are always present, so a caller
/// can always verify that accepted + excluded equals the candidate count.
/// </summary>
public sealed record SegmentResolutionResultDto(
    Guid SegmentId,
    string SegmentCode,
    int SegmentVersion,
    string SegmentType,
    string SubjectType,
    bool Superseded,
    DateTimeOffset EffectiveAt,
    bool SegmentEffective,
    int CandidateCount,
    int MatchedCount,
    int ExcludedCount,
    int TotalMemberCount,
    int Limit,
    int Offset,
    int MaxCandidateSet,
    IReadOnlyList<SegmentMemberDto> Members,
    IReadOnlyList<SegmentMemberDto> Excluded,
    IReadOnlyList<string> ReasonCodes,
    DateTimeOffset ResolvedAt,
    string ResolverVersion);

/// <summary>The single-subject answer (MOD-0167-FU01 section 5 seam). <c>unknown</c> is an answer, never a member.</summary>
public sealed record SegmentMembershipVerdictDto(
    Guid SegmentId,
    string SegmentCode,
    int SegmentVersion,
    string SubjectType,
    Guid SubjectId,
    string? SubjectDisplayName,
    string Verdict,
    string? MembershipSource,
    bool Superseded,
    DateTimeOffset EffectiveAt,
    IReadOnlyList<string> ReasonCodes,
    string ResolverVersion);

/// <summary>Reverse question: which active segments does this subject belong to? Bounded by
/// <c>SegmentLimits.MaxSegmentsPerSubject</c>; beyond it the answer is a 422, never a truncated list.</summary>
public sealed record SubjectSegmentDto(
    Guid SegmentId,
    string SegmentCode,
    string SegmentName,
    int SegmentVersion,
    string SegmentType,
    string Verdict,
    string? MembershipSource,
    IReadOnlyList<string> ReasonCodes);

/// <summary>The catalog as the contract publishes it (D5): a UI builds its attribute / operator / parameter inputs from
/// this and hardcodes nothing.</summary>
public sealed record SegmentAttributeCatalogDto(
    IReadOnlyList<SegmentAttributeDto> Attributes,
    IReadOnlyList<string> ValueSourceKinds,
    IReadOnlyList<string> Classes,
    IReadOnlyList<string> Operators,
    IReadOnlyList<string> ValueTypes,
    int MaxValuesPerInOperator,
    int MaxCriteriaDepth,
    int MaxCriteriaNodes,
    int MaxChildrenPerGroup,
    int DefaultConceptAffinityDepth,
    int MaxConceptAffinityDepth,
    IReadOnlyList<string> ConceptAffinityRelationshipTypes);

/// <summary>One published attribute. <c>Class</c> is the evaluation class (N/J/D); <c>DeclaredClass</c> adds the "+X"
/// marker when the VALUE is additionally proven cross-service.
/// <para><c>Domain</c> is the presentation-only business grouping (the optgroup a criteria editor renders the attribute
/// under, e.g. doctor-profile / consent / workplace). It is descriptive metadata only — it never affects validation or
/// evaluation, so an older UI that ignores it behaves exactly as before.</para>
/// <para><c>ValueSource</c> (P1a) tells an editor where a legitimate value comes from — a published MOD-0048 set, a
/// closed enum, another aggregate's picker, or genuinely free text. It is descriptive: the runtime still accepts any
/// value the validator allows, so an older UI that ignores it behaves exactly as before.</para></summary>
public sealed record SegmentAttributeDto(
    string AttributeCode,
    string Domain,
    string Class,
    string DeclaredClass,
    string Source,
    string ValueType,
    IReadOnlyList<string> Operators,
    IReadOnlyList<string> RequiredParameters,
    IReadOnlyList<string> OptionalParameters,
    IReadOnlyList<string> SubjectTypes,
    bool RequiresCrossServiceValueValidation,
    string? CrossServiceReferenceKind,
    SegmentAttributeValueSourceDto ValueSource,
    IReadOnlyDictionary<string, SegmentAttributeValueSourceDto>? ParameterValueSources = null);

/// <summary>Where an authored value legitimately comes from. <c>kind</c> is the discriminator the UI branches on;
/// only the field matching that kind is populated.</summary>
public sealed record SegmentAttributeValueSourceDto(
    string Kind,
    string? ReferenceSetCode,
    IReadOnlyList<string> AllowedValues,
    string? EntityKind);

/// <summary>The in-process seam verdict (see <c>ISegmentMembershipReader</c>). Same semantics as the HTTP verdict, with
/// no envelope: a consumer never needs raw segment read access.</summary>
public sealed record SegmentMembershipVerdict(
    Guid SegmentId,
    int SegmentVersion,
    string SubjectType,
    Guid SubjectId,
    string Verdict,
    IReadOnlyList<string> ReasonCodes,
    DateTimeOffset EffectiveAt)
{
    public bool IsMember => string.Equals(Verdict, Domain.Entities.SegmentMembershipVerdicts.Member, StringComparison.Ordinal);

    /// <summary>unknown is NEVER member. Stated as code so a consumer cannot get it wrong by accident.</summary>
    public bool IsUnknown => string.Equals(Verdict, Domain.Entities.SegmentMembershipVerdicts.Unknown, StringComparison.Ordinal);
}

/// <summary>The in-process seam resolution (see <c>ISegmentMembershipReader</c>). Bounded, deterministic, persists
/// nothing; it reports and never generates a CampaignTarget or a VisitFrequencyPolicy.</summary>
public sealed record SegmentResolutionResult(
    Guid SegmentId,
    int SegmentVersion,
    string SubjectType,
    bool Superseded,
    DateTimeOffset EffectiveAt,
    int CandidateCount,
    int TotalMemberCount,
    IReadOnlyList<SegmentMemberDto> Members);

/// <summary>
/// The output of the DRAFT-rule <c>preview</c> — the live reach rail. It is a REPORT that persists nothing, produced by
/// the same resolver <c>/resolve</c> uses, only aimed at an in-memory rule instead of a stored one.
/// <para><see cref="TotalCount"/> is what the saved rule would resolve to (the resolver's matched count), so a draft
/// preview and the eventual <c>/resolve</c> of the same rule agree by construction. <see cref="ConditionCounts"/> counts
/// each predicate on its OWN (a one-condition rule): the funnel of "N match this alone" that a matchMode=all rule then
/// narrows. <see cref="SampleMembers"/> is a bounded sample (never the whole list — that is what <c>/resolve</c> paging
/// is for). When the rule is so wide it breaches the candidate ceiling the endpoint answers <b>422</b>, never a partial
/// number, exactly as <c>/resolve</c> does.</para>
/// </summary>
public sealed record SegmentReachPreviewDto(
    string SubjectType,
    string MatchMode,
    DateTimeOffset EffectiveAt,
    int TotalCount,
    int SampleLimit,
    int MaxCandidateSet,
    IReadOnlyList<SegmentReachConditionDto> ConditionCounts,
    IReadOnlyList<SegmentReachSampleMemberDto> SampleMembers,
    DateTimeOffset ResolvedAt,
    string ResolverVersion);

/// <summary>One predicate of the draft rule, counted as if it were the ONLY condition. <see cref="NodeId"/> echoes the
/// id the caller sent for that node (or the runtime-assigned one when the caller omitted it), so the reach rail can line
/// each count up with the editor row it came from.
/// <para><see cref="CapExceeded"/> is true when THIS predicate alone is wider than the candidate ceiling: the honest
/// answer is "more than <see cref="SegmentReachPreviewDto.MaxCandidateSet"/>", and <see cref="Count"/> then carries that
/// ceiling as a floor rather than a precise, and false, total.</para></summary>
public sealed record SegmentReachConditionDto(
    Guid NodeId,
    string? AttributeCode,
    string? Label,
    int Count,
    bool CapExceeded);

/// <summary>One sampled member of the draft rule's reach. It carries only what the resolver already projects onto a
/// member at no extra read — the id and a display label. It is a preview sample, not a member export: the real member
/// list (with paging) is a saved segment's <c>/resolve</c>.
/// <para><see cref="SubjectSecondaryLabel"/> (WP-SEG-D) is the ADDITIVE, nullable second display line copied verbatim
/// from the resolved member (contact → specialty, account → type, optionally " · city"), so the sample can show it
/// under the name instead of a raw id. Appended last; existing fields are unchanged.</para></summary>
public sealed record SegmentReachSampleMemberDto(
    Guid SubjectId,
    string SubjectType,
    string? DisplayName,
    string? SubjectSecondaryLabel = null);

/// <summary>List envelope for the segment grid. Total is the count BEFORE paging, so a UI never has to guess.</summary>
public sealed record SegmentListDto(IReadOnlyList<SegmentListItemDto> Items, int Total);

/// <summary>List envelope for the manual membership rows of one segment.</summary>
public sealed record TargetCustomerListDto(IReadOnlyList<TargetCustomerDto> Items, int Total);

/// <summary>List envelope for the reverse question. Evaluated is how many active segments were actually examined, and
/// it can never exceed the published ceiling: past it the answer is a 422, never a shortened list.</summary>
public sealed record SubjectSegmentListDto(
    string SubjectType,
    Guid SubjectId,
    DateTimeOffset EffectiveAt,
    int Evaluated,
    int MaxSegmentsPerSubject,
    IReadOnlyList<SubjectSegmentDto> Items);
