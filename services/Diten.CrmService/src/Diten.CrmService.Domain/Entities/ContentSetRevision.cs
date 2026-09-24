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

    // ── render output (SCMM-16B; additive) ──────────────────────────────────────────────────────────────────────────
    /// <summary>The rendered PDF artifact bound to this revision (SCMM-16B), or null until it has been rendered. This is
    /// the ONLY field rendering writes — the frozen snapshot and the review status/decision are never mutated by it. It is
    /// written once (idempotent render): a re-render returns the existing pointer rather than producing a second artifact.
    /// </summary>
    public ContentSetRenderedArtifact? RenderedArtifact { get; set; }

    // ── release / managed withdrawal (SCMM-17; additive) ────────────────────────────────────────────────────────────
    /// <summary>The release lifecycle of the rendered artifact (SCMM-17), or null until the revision is released. This is
    /// the ONLY field release/withdrawal writes — the frozen snapshot, the review status/decision and the rendered
    /// artifact are never mutated by it. At release the <see cref="RenderedArtifact"/> ContentId + Checksum are pinned into
    /// the state (manifest-bound), so what was released stays immutably identified. Withdrawal is a managed state change,
    /// not a deletion — the stored bytes are never removed (AD-6).</summary>
    public ContentSetReleaseState? ReleaseState { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;

    /// <summary>True while the revision can still receive a decision (submitted or in-review).</summary>
    public bool IsOpen() => ReviewStatus is ContentSetReviewStatuses.Submitted or ContentSetReviewStatuses.InReview;

    /// <summary>True once a rendered artifact has been bound to this revision (SCMM-16B).</summary>
    public bool IsRendered() => RenderedArtifact is not null;

    /// <summary>True once the rendered artifact has been released (SCMM-17) and not since withdrawn.</summary>
    public bool IsReleased() => ReleaseState is { ReleaseStatus: ContentSetReleaseStatuses.Released };

    /// <summary>True once a released artifact has been withdrawn (SCMM-17). Terminal — a withdrawn revision is never
    /// re-released; a fresh revision is required.</summary>
    public bool IsWithdrawn() => ReleaseState is { ReleaseStatus: ContentSetReleaseStatuses.Withdrawn };
}

/// <summary>
/// SCMM-16B (CAND-CAP-0011, SCMM-16) — the immutable pointer to a revision's rendered PDF, stored through the
/// MOD-0262-FU01 document repository. Bound once after approval; <see cref="ContentId"/> + <see cref="Checksum"/> are the
/// manifest-bound provenance (AT05). The raw storage object key is deliberately NOT held here — FU01 never returns it and
/// download is addressed by content id (non-leakage).
/// </summary>
public sealed class ContentSetRenderedArtifact
{
    /// <summary>The FU01 repository content id the artifact was stored under (server-issued).</summary>
    public Guid ContentId { get; set; }

    /// <summary>The SHA-256 checksum FU01 computed while streaming the bytes (lowercase hex).</summary>
    public string Checksum { get; set; } = string.Empty;

    public string MediaType { get; set; } = "application/pdf";
    public long ByteSize { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTimeOffset RenderedAtUtc { get; set; }
    public string? RenderedBy { get; set; }
}

/// <summary>
/// SCMM-17 (CAND-CAP-0011, SCMM-17) — the release lifecycle of a revision's rendered artifact. Additive to the
/// SCMM-15/16B contract; the frozen snapshot, review and render are never mutated. <b>Manifest-bound (AT05):</b> the
/// released artifact's ContentId + Checksum are pinned here at release, so the release is defined against a specific,
/// immutable output. <b>Managed withdrawal (AT07 / AD-6):</b> withdrawal flips the status and records who/when/why; it
/// never deletes the stored bytes and is terminal (no re-release).
/// </summary>
public sealed class ContentSetReleaseState
{
    /// <summary><see cref="ContentSetReleaseStatuses"/> — released / withdrawn.</summary>
    public string ReleaseStatus { get; set; } = ContentSetReleaseStatuses.Released;

    /// <summary>The pinned released artifact's FU01 content id (manifest-bound).</summary>
    public Guid ReleasedArtifactContentId { get; set; }

    /// <summary>The pinned released artifact's SHA-256 checksum (manifest-bound).</summary>
    public string ReleasedArtifactChecksum { get; set; } = string.Empty;

    public DateTimeOffset ReleasedAtUtc { get; set; }
    public string? ReleasedBy { get; set; }

    // Withdrawal — recorded on a managed withdraw; the pinned release identity above is preserved.
    public DateTimeOffset? WithdrawnAtUtc { get; set; }
    public string? WithdrawnBy { get; set; }
    public string? WithdrawalReason { get; set; }
}

/// <summary>SCMM-17 release lifecycle. <c>withdrawn</c> is terminal (no re-release); there is no delete/purge here
/// (AD-6 — withdrawal is a state, not a destruction).</summary>
public static class ContentSetReleaseStatuses
{
    public const string Released = "released";
    public const string Withdrawn = "withdrawn";
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

    /// <summary>SCMM-16B — a revision was rendered to a PDF artifact and the pointer bound.</summary>
    public const string Rendered = "content_set_revision_rendered";

    /// <summary>SCMM-16B — render was refused because the revision is not approved.</summary>
    public const string NotApproved = "content_set_revision_not_approved";

    /// <summary>SCMM-17 — a rendered revision's artifact was released (manifest-bound).</summary>
    public const string Released = "content_set_revision_released";

    /// <summary>SCMM-17 — a released revision's artifact was withdrawn (managed; bytes retained).</summary>
    public const string Withdrawn = "content_set_revision_withdrawn";

    /// <summary>SCMM-17 — release refused because the revision has no rendered artifact.</summary>
    public const string NotRendered = "content_set_revision_not_rendered";

    /// <summary>SCMM-17 — release refused because the releaser is the reviewer (separation of duties).</summary>
    public const string ReleaseSoD = "content_set_revision_release_sod";
}
