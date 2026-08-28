namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0162 FU04 — KnowledgePath (implements the MOD-0162-FU01A boundary). Answers exactly one question that FU02
/// (single content) and FU03 (how concepts link) leave open: <b>in which order is content told / learned / shown?</b>
/// A path is a <b>template</b>, never a run: whether a step was actually shown, completed or skipped is NOT modelled
/// here (that is MOD-0309 / F-DETAIL). It opens no engine — no branch evaluator, recommendation, best-next-content,
/// completion / progress, AI personalization, digital detailing or visit/route planning.
/// <para>
/// D2 = EMBEDDED: steps are an in-document list (<see cref="Steps"/>), not a separate aggregate — one collection, one
/// <see cref="EntityBase.Version"/> (a step write bumps the path's token). <see cref="EntityBase.Id"/> is the PathId,
/// <see cref="PathCode"/> is the stable business key and <see cref="PathVersion"/> is the business version
/// (<c>Version</c> on <see cref="EntityBase"/> is the concurrency token, never a business field). Closing a path or a
/// step is the soft archive lifecycle; there is no hard delete, and a published version's step set is frozen.
/// </para>
/// </summary>
public sealed class KnowledgePath : EntityBase
{
    /// <summary>Stable business key, shared across the versions of one logical path, unique per tenant among
    /// non-archived rows. Never renamed (rename is done through <see cref="PathName"/>).</summary>
    public string PathCode { get; set; } = string.Empty;

    public string PathName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Required MOD-0162 subject classification. Archived subject accepts no new path.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Optional MOD-0162 topic classification. When supplied it must belong to <see cref="SubjectId"/>.</summary>
    public Guid? TopicId { get; set; }

    /// <summary>Optional generic audience profile. Absent means the path is general — no profile is invented.</summary>
    public Guid? AudienceProfileId { get; set; }

    /// <summary>The path's objective (FU01A §3).</summary>
    public string Objective { get; set; } = string.Empty;

    /// <summary>Optional. Absent means the step content languages decide; a mixed-language path stays visible.</summary>
    public string? LanguageCode { get; set; }

    /// <summary>Business version. NOT <see cref="EntityBase.Version"/> (that is the concurrency token). Several
    /// versions may share one <see cref="PathCode"/>.</summary>
    public string PathVersion { get; set; } = string.Empty;

    /// <summary><see cref="KnowledgePathStatuses"/> — draft / review / approved / published / inactive / archived.</summary>
    public string PathStatus { get; set; } = KnowledgePathStatuses.Draft;

    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>Open-ended when null. EffectiveFrom / EffectiveTo are DateTimeOffset (BSON array): never both used as
    /// index keys nor sorted server-side (parallel-array trap).</summary>
    public DateTimeOffset? EffectiveTo { get; set; }

    /// <summary><see cref="KnowledgePathSources"/> — provenance of how this path was authored.</summary>
    public string Source { get; set; } = KnowledgePathSources.Manual;

    /// <summary>Embedded ordered step list (D2). Archived steps stay in the array as history; they are never removed.</summary>
    public List<KnowledgePathStep> Steps { get; set; } = new();

    /// <summary>Set when the path is published; proof the step set is frozen (§7.1). A frozen version accepts no step
    /// add/update/archive (409) — a change needs a new version.</summary>
    public DateTimeOffset? StepSetFrozenAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }

    /// <summary>The published source a <c>new-version</c> was cloned from (D5). Provenance only, not a chain engine.</summary>
    public Guid? SupersedesPathId { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;

    public bool IsPublished()
        => string.Equals(PathStatus, KnowledgePathStatuses.Published, StringComparison.OrdinalIgnoreCase);

    /// <summary>The step set is frozen once the path is published (StepSetFrozenAt is set at publish time).</summary>
    public bool IsStepSetFrozen() => StepSetFrozenAt is not null;

    public bool IsEffectiveAt(DateTimeOffset at)
        => EffectiveFrom <= at && (EffectiveTo is null || at <= EffectiveTo);

    public IEnumerable<KnowledgePathStep> ActiveSteps() => Steps.Where(s => !s.IsArchived());

    /// <summary>Deterministic read order: StepOrder then StepCode (tie-break). Same order on every read.</summary>
    public IReadOnlyList<KnowledgePathStep> OrderedActiveSteps()
        => ActiveSteps().OrderBy(s => s.StepOrder).ThenBy(s => s.StepCode, StringComparer.OrdinalIgnoreCase).ToList();
}

/// <summary>
/// MOD-0162 FU04 embedded step (<see cref="KnowledgePath.Steps"/>, D2). NOT an aggregate: it has no collection, no
/// <c>TenantId</c>, no <c>EntityBase.Version</c>, no repository and no <c>PathId</c> (the step already lives inside the
/// path document). A step points at a <see cref="ContentId"/> (required) and optionally teaches a FU03
/// <see cref="ConceptNodeId"/> — the master stays the SoR and nothing is copied (only the content's provenance
/// <see cref="ContentCode"/> is carried).
/// </summary>
public sealed class KnowledgePathStep
{
    /// <summary>Generated in the document; unique within the path; re-generated in a <c>new-version</c> copy (D5).</summary>
    public Guid StepId { get; set; } = Guid.NewGuid();

    /// <summary>Unique within the path among ACTIVE steps (409 on duplicate); gaps allowed (10/20/30 suggested).
    /// The DB cannot enforce in-array uniqueness — the handler is the only defence (§4.5).</summary>
    public int StepOrder { get; set; }

    /// <summary>Stable, machine-readable within the path; the tie-break key on equal order.</summary>
    public string StepCode { get; set; } = string.Empty;

    public string StepTitle { get; set; } = string.Empty;

    /// <summary><see cref="KnowledgePathStepTypes"/> (19 values) — in-domain fail-closed.</summary>
    public string StepType { get; set; } = string.Empty;

    /// <summary>Published + effective <see cref="KnowledgeContent"/>. Archived content is rejected on a new/changed value.</summary>
    public Guid ContentId { get; set; }

    /// <summary>Copied from the content on write; the key of latest-published resolution + provenance. The content
    /// itself is never copied.</summary>
    public string ContentCode { get; set; } = string.Empty;

    /// <summary><see cref="KnowledgePathVersionPin"/> — pinned (default) / latest-published.</summary>
    public string VersionPinPolicy { get; set; } = KnowledgePathVersionPin.Pinned;

    public bool IsRequired { get; set; }

    /// <summary><see cref="KnowledgePathCompletionRules"/> — a declaration, never an engine.</summary>
    public string CompletionRule { get; set; } = KnowledgePathCompletionRules.None;

    /// <summary>Same path, smaller StepOrder, no cycle (else 400).</summary>
    public Guid? PrerequisiteStepId { get; set; }

    /// <summary>FU03 ConceptNode.Id; live, non-archived, same tenant (else 400). The node is never mutated.</summary>
    public Guid? ConceptNodeId { get; set; }

    /// <summary>1–600; required when CompletionRule = duration-met (else 400).</summary>
    public int? EstimatedDurationMinutes { get; set; }

    public string? Notes { get; set; }

    /// <summary>Embedded, authorable, but NEVER evaluated (D7). Max 20 per step.</summary>
    public List<KnowledgePathBranchCondition> BranchConditions { get; set; } = new();

    /// <summary><see cref="KnowledgePathStepStatuses"/> — active / archived. Not a form field; changed by the step
    /// archive action (§2.1/S4). An archived step is never removed from the array.</summary>
    public string StepStatus { get; set; } = KnowledgePathStepStatuses.Active;

    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;
}

/// <summary>
/// MOD-0162 FU04 embedded branch condition (D7 — authorable, DATA ONLY). Carried as data and passed to a consumer as
/// data; <b>no branch is ever evaluated</b> (<c>supportsBranchEvaluator</c> is absent from the contract). A path must
/// always be walkable start-to-finish WITHOUT any branch condition (the linear pass is complete — FU01A §8).
/// </summary>
public sealed class KnowledgePathBranchCondition
{
    public string ConditionCode { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>When supplied it must reference a step in the SAME path (else 400; referential sanity, no evaluation).</summary>
    public Guid? TargetStepId { get; set; }
}

/// <summary>Path lifecycle. In-domain (structural) — validated here, never through MOD-0048, so authoring never fails
/// open on an unpublished set. <c>review</c>/<c>approved</c> are future-ready metadata only (real approval is F-WF).
/// Hard delete does not exist; closing a path is the archive endpoint.</summary>
public static class KnowledgePathStatuses
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

/// <summary>How the path was authored. In-domain (structural).</summary>
public static class KnowledgePathSources
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

/// <summary>Step lifecycle (§2.1/S4). Archive is the only removal path; an archived step stays in the document.</summary>
public static class KnowledgePathStepStatuses
{
    public const string Active = "active";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Active, Archived };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Active : value.Trim().ToLowerInvariant();
}

/// <summary>Content version resolution policy (§8.3). Silent version drift is forbidden.</summary>
public static class KnowledgePathVersionPin
{
    public const string Pinned = "pinned";
    public const string LatestPublished = "latest-published";

    public static readonly IReadOnlyList<string> All = new[] { Pinned, LatestPublished };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Pinned : value.Trim().ToLowerInvariant();
}

/// <summary>Completion rule the step DECLARES (never an engine — MOD-0309 measures). <c>assessment-passed</c> is only
/// accepted when the referenced content is a quiz (D6=A).</summary>
public static class KnowledgePathCompletionRules
{
    public const string None = "none";
    public const string Viewed = "viewed";
    public const string Acknowledged = "acknowledged";
    public const string AssessmentPassed = "assessment-passed";
    public const string DurationMet = "duration-met";

    public static readonly IReadOnlyList<string> All = new[] { None, Viewed, Acknowledged, AssessmentPassed, DurationMet };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? None : value.Trim().ToLowerInvariant();
}

/// <summary>In-domain step-type vocabulary (19 values). Fail-closed: a value outside this set is a 400. Covers pharma
/// detailing AND non-pharma (lesson/vocabulary/grammar/…) equally — no pharma type is privileged.</summary>
public static class KnowledgePathStepTypes
{
    public const string Intro = "intro";
    public const string CoreMessage = "core-message";
    public const string ClinicalEvidence = "clinical-evidence";
    public const string Indication = "indication";
    public const string BrandMessage = "brand-message";
    public const string ObjectionHandling = "objection-handling";
    public const string Faq = "faq";
    public const string Practice = "practice";
    public const string Quiz = "quiz";
    public const string Assignment = "assignment";
    public const string Summary = "summary";
    public const string Closing = "closing";
    public const string Lesson = "lesson";
    public const string Vocabulary = "vocabulary";
    public const string Grammar = "grammar";
    public const string Listening = "listening";
    public const string Speaking = "speaking";
    public const string Reading = "reading";
    public const string Homework = "homework";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Intro, CoreMessage, ClinicalEvidence, Indication, BrandMessage, ObjectionHandling, Faq, Practice, Quiz,
        Assignment, Summary, Closing, Lesson, Vocabulary, Grammar, Listening, Speaking, Reading, Homework
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>Content resolution outcome (§8.3) surfaced on every step read — never silent.</summary>
public static class KnowledgePathContentResolutionStatuses
{
    public const string Pinned = "pinned";
    public const string ResolvedLatest = "resolved-latest";
    public const string Unresolved = "unresolved";
}

/// <summary>Canonical FU04 reason codes surfaced on write outcomes and on the contract. Nothing is silent.</summary>
public static class KnowledgePathReasonCodes
{
    public const string Created = "knowledge_path_created";
    public const string Updated = "knowledge_path_updated";
    public const string Published = "knowledge_path_published";
    public const string Archived = "knowledge_path_archived";
    public const string VersionCreated = "knowledge_path_version_created";
    public const string DuplicateCode = "knowledge_path_duplicate_code";

    public const string StepAdded = "knowledge_path_step_added";
    public const string StepUpdated = "knowledge_path_step_updated";
    public const string StepArchived = "knowledge_path_step_archived";
    public const string StepOrderConflict = "knowledge_path_step_order_conflict";
    public const string StepSetFrozen = "knowledge_path_step_set_frozen";
    public const string PrerequisiteInvalid = "knowledge_path_prerequisite_invalid";
    public const string RequiredStepOptionalPrerequisite = "knowledge_path_required_step_optional_prerequisite";
    public const string AssessmentContentNotQuiz = "knowledge_path_assessment_content_not_quiz";
    public const string ContentNotConsumable = "knowledge_path_content_not_consumable";
    public const string ContentUnresolved = "knowledge_path_content_unresolved";
    public const string StepLimitExceeded = "knowledge_path_step_limit_exceeded";
    public const string BranchTargetInvalid = "knowledge_path_branch_target_invalid";
    public const string ArchivedNoMutation = "knowledge_path_archived_no_mutation";
    public const string ReferenceArchived = "knowledge_path_reference_archived";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Created, Updated, Published, Archived, VersionCreated, DuplicateCode,
        StepAdded, StepUpdated, StepArchived, StepOrderConflict, StepSetFrozen, PrerequisiteInvalid,
        RequiredStepOptionalPrerequisite, AssessmentContentNotQuiz, ContentNotConsumable, ContentUnresolved,
        StepLimitExceeded, BranchTargetInvalid, ArchivedNoMutation, ReferenceArchived
    };
}

/// <summary>Document growth limits (§4.2). Published on the contract's limitations list (no surprise).</summary>
public static class KnowledgePathLimits
{
    public const int MaxStepsPerPath = 200;
    public const int MaxBranchConditionsPerStep = 20;
    public const int MinEstimatedDurationMinutes = 1;
    public const int MaxEstimatedDurationMinutes = 600;
}
