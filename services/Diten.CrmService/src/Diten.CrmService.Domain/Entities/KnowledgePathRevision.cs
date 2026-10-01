namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// WP-KP-2 (DESIGN-KP-STUDIO §2.3) — a FROZEN submission of a chain-bound knowledge path to its MOD-0023 MLR review
/// (KP-MLR-{country}: Medical → Legal → Regulatory). One revision = one review round. The path draft stays editable
/// after a submission; the revision never changes again except for its round outcome and its pinned notes.
/// <para>
/// <see cref="RevisionNumber"/> grows per path version (<see cref="PathId"/>); <see cref="EntityBase.CreatedAt"/> /
/// <see cref="CreatedBy"/> are the submitter — the person who may NOT decide on it (D-KP-8, person-based SoD).
/// <see cref="RenderedArtifacts"/> and <see cref="ReleaseState"/> are reserved for KP-3 (render / release) and are not
/// written here.
/// </para>
/// </summary>
public sealed class KnowledgePathRevision : EntityBase
{
    public Guid PathId { get; set; }
    public string PathCode { get; set; } = string.Empty;
    public string PathVersion { get; set; } = string.Empty;
    public int RevisionNumber { get; set; }

    /// <summary><see cref="KnowledgePathRevisionStatuses"/> — mirrors the round: in-review until the outcome lands.</summary>
    public string Status { get; set; } = KnowledgePathRevisionStatuses.InReview;

    /// <summary>The submitter (the caller's stable id — JWT <c>sub</c>). Person-based SoD compares against it.</summary>
    public string? CreatedBy { get; set; }

    public KnowledgePathRevisionSnapshot Snapshot { get; set; } = new();

    /// <summary>The MOD-0023 round of this revision (the shared round shape of the claims flow).</summary>
    public ClaimReviewRound ReviewRound { get; set; } = new();

    public List<KnowledgePathRevisionNote> Notes { get; set; } = new();

    /// <summary>What changed since the previous revision of the same path version (Öneri 6).</summary>
    public KnowledgePathChangeSummary ChangeSummary { get; set; } = new();

    /// <summary>KP-3 — reserved (render outputs of an approved revision). Not written by KP-2.</summary>
    public List<KnowledgePathRenderedArtifact> RenderedArtifacts { get; set; } = new();

    /// <summary>KP-3 — reserved (released / withdrawn). Not written by KP-2.</summary>
    public KnowledgePathReleaseState? ReleaseState { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsOpen() => ReviewRound.IsOpen();
}

/// <summary>WP-KP-2 — the frozen content of a revision. Embedded VO; immutable once written.</summary>
public sealed class KnowledgePathRevisionSnapshot
{
    public Guid ConceptChainTemplateId { get; set; }
    public string ChainVersion { get; set; } = string.Empty;
    public string PathName { get; set; } = string.Empty;
    public string? CountryCode { get; set; }
    public string? LanguageCode { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public List<Guid> AudienceProfileIds { get; set; } = new();
    public List<KnowledgePathRevisionStep> Steps { get; set; } = new();
    public List<KnowledgePathRevisionClaim> Claims { get; set; } = new();
    public List<KnowledgePathRevisionConformance> Conformance { get; set; } = new();

    /// <summary>KP-UI-3 fills pages; empty until then.</summary>
    public List<KnowledgePathRevisionPage> Pages { get; set; } = new();
}

/// <summary>A frozen path step: the content pinned at object + version, its slot and its settings.</summary>
public sealed class KnowledgePathRevisionStep
{
    public Guid StepId { get; set; }
    public string StepCode { get; set; } = string.Empty;
    public string StepTitle { get; set; } = string.Empty;
    public int StepOrder { get; set; }
    public Guid ContentId { get; set; }
    public string ContentCode { get; set; } = string.Empty;
    public string ContentVersion { get; set; } = string.Empty;
    public string? ContentLanguage { get; set; }
    public bool IsRequired { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public Guid? PrerequisiteStepId { get; set; }
    public KnowledgePathArrangement? Arrangement { get; set; }
}

/// <summary>A frozen claim: the record + its version and the path country's version at submission.</summary>
public sealed class KnowledgePathRevisionClaim
{
    public Guid ClaimId { get; set; }
    public string ClaimCode { get; set; } = string.Empty;
    public string ClaimVersion { get; set; } = string.Empty;
    public Guid? CountryVersionId { get; set; }
    public string? CountryVersion { get; set; }
    public string? CountryVersionStatus { get; set; }
    public KnowledgePathArrangement Arrangement { get; set; } = new();
}

/// <summary>A frozen chain conformance row (always ok at submission — the submit gate).</summary>
public sealed class KnowledgePathRevisionConformance
{
    public string BranchCode { get; set; } = string.Empty;
    public Guid ChainStepId { get; set; }
    public int Count { get; set; }
    public int Min { get; set; }
    public int? Max { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>KP-UI-3 placeholder — a designed page (contract only, DESIGN-KP-STUDIO §2.4). Never written by KP-2.</summary>
public sealed class KnowledgePathRevisionPage
{
    public Guid PageId { get; set; }
    public int Order { get; set; }
}

/// <summary>
/// WP-KP-2 — a note pinned on the revision (a page / block / step, optionally at a position). Resolved by its author or
/// a path manager. An unresolved note is carried to the next revision (<see cref="CarriedFromRevision"/>).
/// </summary>
public sealed class KnowledgePathRevisionNote
{
    public Guid NoteId { get; set; } = Guid.NewGuid();
    public string? PageRef { get; set; }
    public string? BlockRef { get; set; }
    public string? StepRef { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Author { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public int? CarriedFromRevision { get; set; }

    public bool IsResolved() => ResolvedAt is not null;
}

/// <summary>WP-KP-2 — the difference to the previous revision (null <see cref="ComparedToRevision"/> on the first).</summary>
public sealed class KnowledgePathChangeSummary
{
    public int? ComparedToRevision { get; set; }
    public List<KnowledgePathChange> Items { get; set; } = new();
}

/// <summary>One change. <see cref="Kind"/> is a <see cref="KnowledgePathChangeKinds"/> value; From / To carry the old
/// and new value (a version, a slot) when the kind has one.</summary>
public sealed class KnowledgePathChange
{
    public string Kind { get; set; } = string.Empty;
    public string Ref { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }
}

public static class KnowledgePathChangeKinds
{
    public const string StepAdded = "step-added";
    public const string StepRemoved = "step-removed";
    public const string StepMoved = "step-moved";
    public const string ContentChanged = "content-changed";
    public const string ContentVersionChanged = "content-version-changed";
    public const string ClaimAdded = "claim-added";
    public const string ClaimRemoved = "claim-removed";
    public const string ClaimMoved = "claim-moved";
    public const string ClaimVersionChanged = "claim-version-changed";
}

/// <summary>KP-3 placeholder — a rendered output of an approved revision. Never written by KP-2.</summary>
public sealed class KnowledgePathRenderedArtifact
{
    public string Kind { get; set; } = string.Empty;
    public Guid ContentId { get; set; }
    public string? Checksum { get; set; }
    public long ByteSize { get; set; }
    public DateTimeOffset RenderedAt { get; set; }
}

/// <summary>KP-3 placeholder — the release state of a revision. Never written by KP-2.</summary>
public sealed class KnowledgePathReleaseState
{
    public string State { get; set; } = string.Empty;
    public DateTimeOffset At { get; set; }
    public string? By { get; set; }
    public string? Reason { get; set; }
}

/// <summary>WP-KP-2 — revision lifecycle (the round outcome).</summary>
public static class KnowledgePathRevisionStatuses
{
    public const string InReview = "in-review";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Withdrawn = "withdrawn";
    public const string TimedOut = "timed-out";

    public static string FromOutcome(string outcome) => outcome switch
    {
        ClaimReviewOutcomes.Approved => Approved,
        ClaimReviewOutcomes.Rejected => Rejected,
        ClaimReviewOutcomes.Cancelled => Withdrawn,
        _ => TimedOut
    };
}

/// <summary>WP-KP-2 — coded review failures of a knowledge path (rendered as the <c>[code, message]</c> pair). The shared
/// workflow codes (approval_template_missing, workflow_unavailable, approval_forbidden, approval_via_workflow_only,
/// no_open_review, withdraw_not_possible, invalid_status) are the claims ones.</summary>
public static class KnowledgePathReviewErrors
{
    public const string ReviewRoundOpen = "review_round_open";
    public const string ChainConformanceFailed = "chain_conformance_failed";
    public const string ComponentNotPublished = "component_not_published";
    public const string ClaimNoCountryVersion = "claim_no_country_version";
    public const string CommentRequired = "comment_required";
    public const string SodSubmitterCannotDecide = "sod_submitter_cannot_decide";
    public const string DecisionInvalid = "decision_invalid";
    public const string NoteNotFound = "note_not_found";
    public const string NoteTextRequired = "note_text_required";
}
