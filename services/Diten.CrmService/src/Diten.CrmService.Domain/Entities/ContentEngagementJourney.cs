namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0162 FU05 — ContentEngagementJourney (implements the MOD-0162-FU01B boundary, canonical name F1 2026-08-26).
/// Answers exactly one question FU02 (single content), FU03 (how concepts link) and FU04 (order inside ONE session)
/// leave open: <b>across several visits / sessions, which stage comes next and which path applies in it?</b>
/// A journey is a <b>template</b>, never a run: current stage, progress, advancement, target assignment and visit
/// execution are NOT modelled here (that is MOD-0155 / MOD-0309 / F-DETAIL). It opens no engine — no advancement or
/// branch evaluator, no recommendation, no completion tracking, no campaign / frequency / segmentation engine.
/// <para>
/// S2 = EMBEDDED (FU04/D2 pattern): stages are an in-document list (<see cref="Stages"/>), not a separate aggregate —
/// one collection, one <see cref="EntityBase.Version"/> (a stage write bumps the journey's token).
/// <see cref="EntityBase.Id"/> is the JourneyId, <see cref="JourneyCode"/> is the stable business key and
/// <see cref="JourneyVersion"/> is the business version (<c>Version</c> on <see cref="EntityBase"/> is the concurrency
/// token, never a business field). Closing a journey or a stage is the soft archive lifecycle; there is no hard delete,
/// and a published version's stage set is frozen.
/// </para>
/// <para>
/// This is NOT MOD-0166 "Journeys &amp; Automation": no trigger, no action, no channel, no suppression, no run log
/// (FU01B §2.1). Campaign / Brand / Product / Segment references are deliberately absent (§2.1/S6 — F-CAMPAIGN-LINK).
/// </para>
/// </summary>
public sealed class ContentEngagementJourney : EntityBase
{
    /// <summary>Stable business key, shared across the versions of one logical journey, unique per tenant among
    /// non-archived rows. Never renamed (rename is done through <see cref="JourneyName"/>).</summary>
    public string JourneyCode { get; set; } = string.Empty;

    public string JourneyName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>REQUIRED (FU01B §3): a journey always belongs to a subject. FU02 Subject — read-only reference.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Optional FU02 Topic; when supplied it must belong to <see cref="SubjectId"/>.</summary>
    public Guid? TopicId { get; set; }

    /// <summary>Optional FU02 AudienceProfile; when absent the journey is general (no profile is invented).</summary>
    public Guid? AudienceProfileId { get; set; }

    /// <summary>What the journey is for (e.g. "Almiba prescribing intent", "complete A1 level").</summary>
    public string Objective { get; set; } = string.Empty;

    /// <summary>Optional; when absent the stages' path languages decide and a mixed-language journey stays VISIBLE.</summary>
    public string? LanguageCode { get; set; }

    /// <summary>BUSINESS version (§2.1/S1) — several versions live under one <see cref="JourneyCode"/>.
    /// <see cref="EntityBase.Version"/> stays the technical concurrency token.</summary>
    public string JourneyVersion { get; set; } = string.Empty;

    public string JourneyStatus { get; set; } = ContentEngagementJourneyStatuses.Draft;

    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>Null = open ended.</summary>
    public DateTimeOffset? EffectiveTo { get; set; }

    public string Source { get; set; } = ContentEngagementJourneySources.Manual;

    /// <summary>EMBEDDED stage list (S2). There is no second collection and no stage repository.</summary>
    public List<ContentEngagementJourneyStage> Stages { get; set; } = new();

    /// <summary>Set at publish time; the stage set is frozen from then on (FU01B §5.1).</summary>
    public DateTimeOffset? StageSetFrozenAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }

    /// <summary>Provenance of a <c>new-version</c> clone — NOT a chain engine.</summary>
    public Guid? SupersedesJourneyId { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;

    public bool IsPublished()
        => string.Equals(JourneyStatus, ContentEngagementJourneyStatuses.Published, StringComparison.OrdinalIgnoreCase);

    /// <summary>The stage set is frozen once the journey is published (StageSetFrozenAt is set at publish time).</summary>
    public bool IsStageSetFrozen() => StageSetFrozenAt is not null;

    public bool IsEffectiveAt(DateTimeOffset at)
        => EffectiveFrom <= at && (EffectiveTo is null || at <= EffectiveTo);

    public IEnumerable<ContentEngagementJourneyStage> ActiveStages() => Stages.Where(s => !s.IsArchived());

    /// <summary>Deterministic read order: StageOrder then StageCode (tie-break). Same order on every read.</summary>
    public IReadOnlyList<ContentEngagementJourneyStage> OrderedActiveStages()
        => ActiveStages().OrderBy(s => s.StageOrder).ThenBy(s => s.StageCode, StringComparer.OrdinalIgnoreCase).ToList();
}

/// <summary>
/// MOD-0162 FU05 embedded stage (<see cref="ContentEngagementJourney.Stages"/>, S2). NOT an aggregate: it has no
/// collection, no <c>TenantId</c>, no <c>EntityBase.Version</c>, no repository and no <c>JourneyId</c> (the stage
/// already lives inside the journey document). A stage BINDS to a published + effective FU04
/// <see cref="RecommendedKnowledgePathId"/> and <b>never copies the path's steps</b> — only the path's provenance
/// <see cref="PathCode"/> is carried (§2.2/AC-FU04-4).
/// </summary>
public sealed class ContentEngagementJourneyStage
{
    /// <summary>Generated in-document; unique inside the journey; regenerated on a <c>new-version</c> clone.</summary>
    public Guid StageId { get; set; } = Guid.NewGuid();

    /// <summary>Unique among ACTIVE stages of the journey (else 409). Gaps are allowed (10/20/30).</summary>
    public int StageOrder { get; set; }

    /// <summary>Stable machine-readable code inside the journey; tie-break key on equal order.</summary>
    public string StageCode { get; set; } = string.Empty;

    public string StageName { get; set; } = string.Empty;

    public string StageObjective { get; set; } = string.Empty;

    /// <summary>Optional in-domain vocabulary (FU01B §5.2 marks the stage-type set optional).</summary>
    public string? StageType { get; set; }

    /// <summary>REQUIRED: a published + effective FU04 KnowledgePath (else 400). The path's steps are NEVER copied.</summary>
    public Guid RecommendedKnowledgePathId { get; set; }

    /// <summary>Copied from the path at write time: the key for <c>latest-published</c> resolution + provenance.</summary>
    public string PathCode { get; set; } = string.Empty;

    /// <summary><c>pinned</c> (default) or <c>latest-published</c> — §8.3. Silent version drift is forbidden.</summary>
    public string PathVersionPinPolicy { get; set; } = ContentEngagementJourneyPathPin.Pinned;

    /// <summary>A published journey must carry at least one ACTIVE required stage (FU01B §4).</summary>
    public bool IsRequired { get; set; }

    /// <summary>Default false (FU01B §7): true = the same stage may be applied in more than one visit/session.</summary>
    public bool Repeatable { get; set; }

    /// <summary>Boundary metadata only (§2.1/S7) — no runtime scheduling.</summary>
    public int? MinVisitNumber { get; set; }

    /// <summary>Boundary metadata only; must be >= <see cref="MinVisitNumber"/>.</summary>
    public int? MaxVisitNumber { get; set; }

    /// <summary>Optional / future metadata — DECLARED, never evaluated (FU01B §6).</summary>
    public string? AdvancementRule { get; set; }

    /// <summary>Must reference another stage of the SAME journey (never itself). May point BACKWARDS (objection →
    /// reinforcement). Carried as data; never interpreted.</summary>
    public Guid? FallbackStageId { get; set; }

    /// <summary>Embedded repeater (S5) — authorable, data only; max 20 per stage.</summary>
    public List<ContentEngagementJourneyBranchCondition> BranchConditions { get; set; } = new();

    public string? Notes { get; set; }

    /// <summary>Stage lifecycle (§2.1/S4) — archive is the only removal path; the element stays in the document.</summary>
    public string StageStatus { get; set; } = ContentEngagementJourneyStageStatuses.Active;

    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;
}

/// <summary>
/// MOD-0162 FU05 embedded branch condition (S5 — authorable, DATA ONLY, aligned with FU04/D7). Carried as data and
/// passed to a consumer as data; <b>no condition is ever evaluated</b> (<c>supportsBranchEvaluator</c> and
/// <c>supportsStageAdvancementEngine</c> are absent from the contract). A journey must always be walkable start-to-finish
/// WITHOUT any branch condition, advancement rule or fallback (the linear pass is complete — FU01B §6).
/// </summary>
public sealed class ContentEngagementJourneyBranchCondition
{
    public string ConditionCode { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>When supplied it must reference a stage in the SAME journey (else 400; referential sanity, no evaluation).</summary>
    public Guid? TargetStageId { get; set; }
}

/// <summary>Journey lifecycle. In-domain (D-VOCAB = A) — validated here, never through MOD-0048, so authoring never
/// fails open on an unpublished set. <c>review</c>/<c>approved</c> are future-ready metadata only (real approval is
/// F-WF). Hard delete does not exist; closing a journey is the archive endpoint.</summary>
public static class ContentEngagementJourneyStatuses
{
    public const string Draft = "draft";
    public const string Review = "review";
    public const string Approved = "approved";
    public const string Published = "published";
    public const string Inactive = "inactive";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Review, Approved, Published, Inactive, Archived };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Draft : value.Trim().ToLowerInvariant();
}

/// <summary>How the journey was authored. In-domain (structural).</summary>
public static class ContentEngagementJourneySources
{
    public const string Manual = "manual";
    public const string Campaign = "campaign";
    public const string Training = "training";
    public const string LegacyImport = "legacy-import";
    public const string External = "external";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = new[] { Manual, Campaign, Training, LegacyImport, External, Other };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Manual : value.Trim().ToLowerInvariant();
}

/// <summary>Stage lifecycle (§2.1/S4). Archive is the only removal path; an archived stage stays in the document.</summary>
public static class ContentEngagementJourneyStageStatuses
{
    public const string Active = "active";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Active, Archived };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Active : value.Trim().ToLowerInvariant();
}

/// <summary>KnowledgePath version resolution policy (§8.3). Silent version drift is forbidden.</summary>
public static class ContentEngagementJourneyPathPin
{
    public const string Pinned = "pinned";
    public const string LatestPublished = "latest-published";

    public static readonly IReadOnlyList<string> All = new[] { Pinned, LatestPublished };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Pinned : value.Trim().ToLowerInvariant();
}

/// <summary>Advancement rule the stage DECLARES (never an engine — FU01B §6; MOD-0309 / F-DETAIL measure). Values come
/// from the boundary's own examples. Fail-closed: a value outside this set is a 400.</summary>
public static class ContentEngagementJourneyAdvancementRules
{
    public const string None = "none";
    public const string VisitCompleted = "visit-completed";
    public const string RequiredStepsAcknowledged = "required-steps-acknowledged";
    public const string ObjectionRecorded = "objection-recorded";
    public const string AssessmentPassed = "assessment-passed";
    public const string ManagerManual = "manager-manual";
    public const string RepeatUntilConditionMet = "repeat-until-condition-met";

    public static readonly IReadOnlyList<string> All = new[]
    {
        None, VisitCompleted, RequiredStepsAcknowledged, ObjectionRecorded, AssessmentPassed, ManagerManual,
        RepeatUntilConditionMet
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>In-domain stage-type vocabulary (12 values, OPTIONAL field). Fail-closed: a value outside this set is a
/// 400. Covers pharma engagement AND non-pharma (onboarding/lesson/practice/…) equally — no pharma type is privileged.</summary>
public static class ContentEngagementJourneyStageTypes
{
    public const string Awareness = "awareness";
    public const string Interest = "interest";
    public const string ClinicalEvidence = "clinical-evidence";
    public const string ObjectionHandling = "objection-handling";
    public const string Reinforcement = "reinforcement";
    public const string Commitment = "commitment";
    public const string FollowUp = "follow-up";
    public const string Onboarding = "onboarding";
    public const string Lesson = "lesson";
    public const string Practice = "practice";
    public const string Assessment = "assessment";
    public const string Closing = "closing";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Awareness, Interest, ClinicalEvidence, ObjectionHandling, Reinforcement, Commitment, FollowUp, Onboarding,
        Lesson, Practice, Assessment, Closing
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>Path resolution outcome (§8.3) surfaced on every stage read — never silent.</summary>
public static class ContentEngagementJourneyPathResolutionStatuses
{
    public const string Pinned = "pinned";
    public const string ResolvedLatest = "resolved-latest";
    public const string Unresolved = "unresolved";
}

/// <summary>Canonical FU05 reason codes surfaced on write outcomes and on the contract. Nothing is silent.</summary>
public static class ContentEngagementJourneyReasonCodes
{
    public const string Created = "content_engagement_journey_created";
    public const string Updated = "content_engagement_journey_updated";
    public const string Published = "content_engagement_journey_published";
    public const string Archived = "content_engagement_journey_archived";
    public const string VersionCreated = "content_engagement_journey_version_created";
    public const string DuplicateCode = "content_engagement_journey_duplicate_code";
    public const string OverlappingPublishedVersion = "content_engagement_journey_overlapping_published_version";

    public const string StageAdded = "content_engagement_journey_stage_added";
    public const string StageUpdated = "content_engagement_journey_stage_updated";
    public const string StageArchived = "content_engagement_journey_stage_archived";
    public const string StageOrderConflict = "content_engagement_journey_stage_order_conflict";
    public const string StageSetFrozen = "content_engagement_journey_stage_set_frozen";
    public const string NoRequiredStage = "content_engagement_journey_no_required_stage";
    public const string FallbackInvalid = "content_engagement_journey_fallback_invalid";
    public const string BranchTargetInvalid = "content_engagement_journey_branch_target_invalid";
    public const string PathNotConsumable = "content_engagement_journey_path_not_consumable";
    public const string PathUnresolved = "content_engagement_journey_path_unresolved";
    public const string VisitRangeInvalid = "content_engagement_journey_visit_range_invalid";
    public const string StageLimitExceeded = "content_engagement_journey_stage_limit_exceeded";
    public const string ArchivedNoMutation = "content_engagement_journey_archived_no_mutation";
    public const string ReferenceArchived = "content_engagement_journey_reference_archived";
    public const string RuntimeStateNotSupported = "content_engagement_journey_runtime_state_not_supported";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Created, Updated, Published, Archived, VersionCreated, DuplicateCode, OverlappingPublishedVersion,
        StageAdded, StageUpdated, StageArchived, StageOrderConflict, StageSetFrozen, NoRequiredStage, FallbackInvalid,
        BranchTargetInvalid, PathNotConsumable, PathUnresolved, VisitRangeInvalid, StageLimitExceeded,
        ArchivedNoMutation, ReferenceArchived, RuntimeStateNotSupported
    };
}

/// <summary>Document growth limits (§4.2). Published on the contract's limitations list (no surprise).</summary>
public static class ContentEngagementJourneyLimits
{
    public const int MaxStagesPerJourney = 100;
    public const int MaxBranchConditionsPerStage = 20;
    public const int MinVisitNumber = 1;
}
