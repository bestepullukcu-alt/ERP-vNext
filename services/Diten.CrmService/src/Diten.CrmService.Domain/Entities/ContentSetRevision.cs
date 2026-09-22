namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// SCMM-15 (CAND-CAP-0011, DEC-SCMM-04 C3) — a <b>ContentSetRevision</b>: the immutable frozen manifest a
/// <see cref="ContentSet"/> draft produces when it is submitted for review. Where <see cref="ContentSet"/> is the
/// mutable draft (no approved/frozen state), a Revision is the point-in-time, byte-for-byte <b>snapshot</b> of that
/// draft's Template/Scope/SelectedComponents/SelectedClaims/EligibilitySnapshot, pinned to the source
/// <see cref="ContentSetVersion"/>. The snapshot is written once at submit and <b>never mutated afterwards</b>; only the
/// in-domain <see cref="ReviewStatus"/> and <see cref="Decision"/> advance
/// (<c>submitted → in-review → approved | rejected</c>). Release / activate / withdraw (SCMM-17) and render (SCMM-16)
/// are NOT here, so there is no released / withdrawn state. There is no delete (archive-only).
/// <para>
/// Separation of duties (DEC-SCMM-04): the author who submitted a revision may not decide it —
/// <see cref="ContentSetReviewDecision.ReviewerId"/> must differ from <see cref="SubmittedBy"/>.
/// </para>
/// </summary>
public sealed class ContentSetRevision : EntityBase
{
    /// <summary>Stable business key of the revision (e.g. "SET-1-R2").</summary>
    public string RevisionCode { get; set; } = string.Empty;

    /// <summary>Monotonic per-ContentSet lineage number (max existing for the set + 1).</summary>
    public int RevisionNumber { get; set; }

    /// <summary>The source draft this revision was frozen from.</summary>
    public Guid ContentSetId { get; set; }

    /// <summary>The source draft's <see cref="EntityBase.Version"/> at freeze time (pinned provenance).</summary>
    public int ContentSetVersion { get; set; }

    // ── frozen snapshot (written once at submit; immutable) ─────────────────────────────────────────────────────────
    /// <summary>Frozen pinned composition-template reference (copied from the source draft).</summary>
    public ContentSetTemplateRef Template { get; set; } = new();

    /// <summary>Frozen pinned content-scope reference, or null when the draft bound no scope.</summary>
    public ContentSetScopeRef? Scope { get; set; }

    /// <summary>Frozen selected components (deep copy of the draft's selections; SelectionIds preserved).</summary>
    public List<ContentSetComponent> SelectedComponents { get; set; } = new();

    /// <summary>Frozen selected claims (deep copy of the draft's selections; SelectionIds preserved).</summary>
    public List<ContentSetClaim> SelectedClaims { get; set; } = new();

    /// <summary>Frozen non-blocking eligibility snapshot as it stood at submit, or null.</summary>
    public ContentSetEligibilitySnapshot? EligibilitySnapshot { get; set; }

    // ── in-domain review ────────────────────────────────────────────────────────────────────────────────────────────
    /// <summary><see cref="ContentSetReviewStatuses"/> — submitted / in-review / approved / rejected.</summary>
    public string ReviewStatus { get; set; } = ContentSetReviewStatuses.Submitted;

    /// <summary>The review outcome, null until a decision is recorded.</summary>
    public ContentSetReviewDecision? Decision { get; set; }

    public string? SubmittedBy { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }

    /// <summary>Correlates the submission with its decision (DEC-SCMM-04 duplicate/stale-safe correlation).</summary>
    public string? CorrelationId { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;

    /// <summary>True while the revision can still receive a decision (submitted or in-review).</summary>
    public bool IsOpen() => ReviewStatus is ContentSetReviewStatuses.Submitted or ContentSetReviewStatuses.InReview;
}

/// <summary>SCMM-15 — the recorded review outcome of a revision. Embedded VO. <see cref="ReviewerId"/> is the deciding
/// actor (server-resolved), which SoD requires to differ from the revision's submitter.</summary>
public sealed class ContentSetReviewDecision
{
    public string? ReviewerId { get; set; }
    public string Decision { get; set; } = string.Empty;   // ContentSetReviewDecisions (approve → approved / reject → rejected)
    public string? Reason { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
}

/// <summary>SCMM-15 review lifecycle (in-domain, structural). No released / withdrawn (SCMM-17). No hard delete
/// (archive-only).</summary>
public static class ContentSetReviewStatuses
{
    public const string Submitted = "submitted";
    public const string InReview = "in-review";
    public const string Approved = "approved";
    public const string Rejected = "rejected";

    public static readonly IReadOnlyList<string> All = new[] { Submitted, InReview, Approved, Rejected };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());
}

/// <summary>SCMM-15 review-decision inputs (the caller says approve / reject; the status becomes approved / rejected).</summary>
public static class ContentSetReviewDecisions
{
    public const string Approve = "approve";
    public const string Reject = "reject";

    public static readonly IReadOnlyList<string> All = new[] { Approve, Reject };

    /// <summary>Maps an input verb to the terminal <see cref="ContentSetReviewStatuses"/>, or null when unknown.</summary>
    public static string? ToStatus(string? decision)
    {
        var d = decision?.Trim().ToLowerInvariant();
        return d switch
        {
            Approve => ContentSetReviewStatuses.Approved,
            Reject => ContentSetReviewStatuses.Rejected,
            _ => null
        };
    }
}

/// <summary>Canonical SCMM-15 revision reason / outcome codes surfaced on write outcomes and audit.</summary>
public static class ContentSetRevisionReasonCodes
{
    public const string Submitted = "content_set_revision_submitted";
    public const string Approved = "content_set_revision_approved";
    public const string Rejected = "content_set_revision_rejected";
}
