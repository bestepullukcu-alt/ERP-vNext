namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0162 FU02 — KnowledgeContent. Answers <b>one</b> question: "what is this piece of content, what does it teach /
/// present, for which subject-topic-audience, which version is valid, and when?" It deliberately does NOT answer
/// "in which order?" (MOD-0162-FU01A KnowledgePath), "across which visits?" (MOD-0162-FU01B EngagementJourney),
/// "which concept chain?" (MOD-0162-FU01C — only referenced by id here), "how often to visit?" (MOD-0165/0167),
/// "who/when to visit?" (MOD-0155) or "may we contact them?" (MOD-0164).
/// <para>
/// Content is NOT modelled as Brand/Product content — Brand/Product are optional <b>references</b> (MOD-0290 / MDM is
/// the master; nothing is copied here). Non-pharma subjects (language courses, QMS/SOP training, onboarding) are
/// first-class. A binary file is never stored here: <see cref="FileRef"/> points at a MOD-0028/0029 document.
/// </para>
/// <para>
/// <see cref="EntityBase.Id"/> is the ContentId and <see cref="ContentCode"/> is the stable business key (rename is done
/// through <see cref="ContentTitle"/> only). The business version is <see cref="ContentVersion"/> — <c>Version</c> on
/// <see cref="EntityBase"/> is the technical concurrency token, never a business field. Closing content is the soft
/// <see cref="ArchivedAt"/> lifecycle; there is no hard delete, and archived content accepts no update.
/// </para>
/// </summary>
public sealed class KnowledgeContent : EntityBase
{
    /// <summary>Stable business key, shared across the versions of one logical content, unique per tenant among
    /// non-archived rows. Never renamed.</summary>
    public string ContentCode { get; set; } = string.Empty;

    public string ContentTitle { get; set; } = string.Empty;

    /// <summary><see cref="KnowledgeContentTypes"/> — in-domain (structural) vocabulary.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary><see cref="KnowledgeContentStatuses"/> — draft / review / approved / published / inactive / archived.</summary>
    public string ContentStatus { get; set; } = KnowledgeContentStatuses.Draft;

    /// <summary>Required MOD-0162 subject classification. Archived subject accepts no new content.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Optional MOD-0162 topic classification. When supplied it must belong to <see cref="SubjectId"/>.</summary>
    public Guid? TopicId { get; set; }

    /// <summary>Optional generic audience profile. Absent means the content is general — no profile is invented.</summary>
    public Guid? AudienceProfileId { get; set; }

    /// <summary>Optional MOD-0162-FU01C concept node reference — format level only. No concept graph is traversed here.</summary>
    public Guid? ConceptNodeId { get; set; }

    /// <summary>Optional MOD-0290 brand reference. Absent for non-pharma content, which stays fully valid.</summary>
    public Guid? BrandId { get; set; }

    /// <summary>Optional MOD-0290 product reference.</summary>
    public Guid? ProductId { get; set; }

    /// <summary>Optional MOD-0165 campaign metadata. Campaign runtime is never mutated from here.</summary>
    public Guid? CampaignId { get; set; }

    /// <summary>Optional MOD-0167 segment metadata.</summary>
    public Guid? SegmentId { get; set; }

    /// <summary>Content language. Several languages may share one <see cref="ContentCode"/>.</summary>
    public string LanguageCode { get; set; } = string.Empty;

    public string? Summary { get; set; }

    /// <summary>Pointer to a structured body (e.g. an HTML/markdown record key). A pointer, never inline content.</summary>
    public string? ContentBodyRef { get; set; }

    /// <summary>Pointer to a rendered asset. A pointer, never a stored binary.</summary>
    public string? ContentAssetRef { get; set; }

    /// <summary>MOD-0028/0029 document reference (documentId + versionId form). The document store is not opened here.</summary>
    public string? FileRef { get; set; }

    /// <summary>External URL. One of Body/Asset/File/Url must be present.</summary>
    public string? Url { get; set; }

    /// <summary>Business version. NOT <see cref="EntityBase.Version"/> (that is the concurrency token).</summary>
    public string ContentVersion { get; set; } = string.Empty;

    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>Open-ended when null.</summary>
    public DateTimeOffset? EffectiveTo { get; set; }

    /// <summary><see cref="KnowledgeContentSources"/> — provenance of how this content was authored.</summary>
    public string Source { get; set; } = KnowledgeContentSources.Manual;

    /// <summary>Free tags. Never a substitute for the subject/topic taxonomy.</summary>
    public List<string> Tags { get; set; } = new();

    public List<KnowledgeExternalReference> ExternalReferences { get; set; } = new();

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;

    /// <summary>Available for consumption: published status AND effective at the instant. Read-only helper; this class
    /// draws no visit/route/recommendation conclusion.</summary>
    public bool IsConsumableAt(DateTimeOffset at)
        => string.Equals(ContentStatus, KnowledgeContentStatuses.Published, StringComparison.OrdinalIgnoreCase)
           && !IsArchived()
           && EffectiveFrom <= at
           && (EffectiveTo is null || at <= EffectiveTo);
}

/// <summary>
/// External / legacy identity carried by a knowledge aggregate. Same six-field contract as the Campaign / Consent /
/// Brand-Product external reference (<c>SourceSystem</c> · <c>ExternalId</c> · <c>ExternalCode</c> · <c>ExternalName</c>
/// · <c>ImportedAt</c> · <c>IsPrimary</c>). Declared separately on purpose — coupling modules through a shared value
/// object would force edits across features; unifying the declarations is a documented follow-up.
/// </summary>
public sealed class KnowledgeExternalReference
{
    public string SourceSystem { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string? ExternalCode { get; set; }
    public string? ExternalName { get; set; }
    public DateTimeOffset? ImportedAt { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>What kind of content this is. In-domain (structural) vocabulary — validated here rather than through MOD-0048,
/// so the runtime never fails open on an unpublished set. Surfaced on the contract so an authoring UI needs no hardcoded
/// list. MOD-0048 publish is a separate operator follow-up (F-RD).</summary>
public static class KnowledgeContentTypes
{
    public const string Presentation = "presentation";
    public const string Brochure = "brochure";
    public const string Lesson = "lesson";
    public const string Faq = "faq";
    public const string ClinicalSummary = "clinical-summary";
    public const string ObjectionHandling = "objection-handling";
    public const string Quiz = "quiz";
    public const string Video = "video";
    public const string Pdf = "pdf";
    public const string HtmlDetail = "html-detail";
    public const string Sop = "sop";
    public const string TrainingMaterial = "training-material";
    public const string MessageScript = "message-script";
    public const string KnowledgeArticle = "knowledge-article";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Presentation, Brochure, Lesson, Faq, ClinicalSummary, ObjectionHandling, Quiz, Video, Pdf, HtmlDetail, Sop,
        TrainingMaterial, MessageScript, KnowledgeArticle
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>Content lifecycle. Hard delete does not exist. In-domain (structural). <c>review</c>/<c>approved</c> are
/// future-ready metadata only — no workflow is opened in FU02.</summary>
public static class KnowledgeContentStatuses
{
    public const string Draft = "draft";
    public const string Review = "review";
    public const string Approved = "approved";
    public const string Published = "published";
    public const string Inactive = "inactive";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Draft, Review, Approved, Published, Inactive, Archived
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Draft : value.Trim().ToLowerInvariant();
}

/// <summary>How the content was authored. In-domain (structural).</summary>
public static class KnowledgeContentSources
{
    public const string Manual = "manual";
    public const string Campaign = "campaign";
    public const string LegacyImport = "legacy-import";
    public const string Training = "training";
    public const string External = "external";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Manual, Campaign, LegacyImport, Training, External, Other
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Manual : value.Trim().ToLowerInvariant();
}

/// <summary>
/// Canonical FU02 reason codes surfaced on write outcomes and audit. Nothing in this feature is silent.
/// </summary>
public static class KnowledgeReasonCodes
{
    public const string ContentCreated = "knowledge_content_created";
    public const string ContentUpdated = "knowledge_content_updated";
    public const string ContentArchived = "knowledge_content_archived";
    public const string ContentDuplicateCode = "knowledge_content_duplicate_code";

    public const string SubjectCreated = "knowledge_subject_created";
    public const string SubjectUpdated = "knowledge_subject_updated";
    public const string SubjectArchived = "knowledge_subject_archived";
    public const string SubjectDuplicateCode = "knowledge_subject_duplicate_code";

    public const string TopicCreated = "knowledge_topic_created";
    public const string TopicUpdated = "knowledge_topic_updated";
    public const string TopicArchived = "knowledge_topic_archived";
    public const string TopicDuplicateCode = "knowledge_topic_duplicate_code";
    public const string TopicCrossSubjectParent = "knowledge_topic_cross_subject_parent";
    public const string TopicParentCycle = "knowledge_topic_parent_cycle";

    public const string AudienceProfileCreated = "knowledge_audience_profile_created";
    public const string AudienceProfileUpdated = "knowledge_audience_profile_updated";
    public const string AudienceProfileArchived = "knowledge_audience_profile_archived";
    public const string AudienceProfileDuplicateCode = "knowledge_audience_profile_duplicate_code";

    public const string ArchivedNoMutation = "knowledge_archived_no_mutation";
    public const string ReferenceArchived = "knowledge_reference_archived";
}
