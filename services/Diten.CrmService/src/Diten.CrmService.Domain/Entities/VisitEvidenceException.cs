namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// WP-VW-W3 (W3-BE-c, §2.3) — a TIME-BOXED relaxation of the tenant's visit evidence rules for ONE rep (no permanent
/// per-rep setting). Approved through MOD-0023 + the Work Center (objectType <c>crm.visit-evidence-exception</c>).
/// <para><b>Lifecycle:</b> <c>draft</c> → <c>in_review</c> → <c>approved</c> → <c>cancelled</c> (a planned one) |
/// <c>ended_early</c> (an active one; its end becomes today). A returned / cancelled / timed-out round → <c>draft</c>.
/// The phase of an approved exception is DERIVED from the UTC day: <c>planned</c> | <c>active</c> | <c>expired</c>.</para>
/// <para><b>Dates:</b> UTC today ≤ start ≤ end and at most 30 days, both ends included. No two in-review / approved
/// exceptions of the same rep relax the same rule on overlapping days.</para>
/// </summary>
public sealed class VisitEvidenceException : EntityBase, IWorkflowApprovable
{
    /// <summary>The rep (the territory resource id the visit execution compares with).</summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>Display snapshot of the rep (never a match key).</summary>
    public string? RepDisplayName { get; set; }

    /// <summary><see cref="VisitEvidenceExceptionRules"/> — at least one.</summary>
    public List<string> Rules { get; set; } = new();

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = VisitEvidenceExceptionStatuses.Draft;

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancelReason { get; set; }

    public List<ClaimReviewRound> ReviewRounds { get; set; } = new();
    public List<RegulatoryTextDecision> Decisions { get; set; } = new();

    public bool IsDraft() => string.Equals(Status, VisitEvidenceExceptionStatuses.Draft, StringComparison.Ordinal);
    public bool IsInReview() => string.Equals(Status, VisitEvidenceExceptionStatuses.InReview, StringComparison.Ordinal);
    public bool IsApproved() => string.Equals(Status, VisitEvidenceExceptionStatuses.Approved, StringComparison.Ordinal);

    /// <summary>The phase of an APPROVED exception on <paramref name="today"/> (UTC day); null for any other status.</summary>
    public string? PhaseOn(DateOnly today)
        => !IsApproved() ? null
            : today < StartDate ? VisitEvidenceExceptionPhases.Planned
            : today > EndDate ? VisitEvidenceExceptionPhases.Expired
            : VisitEvidenceExceptionPhases.Active;

    /// <summary>Counts against a new one (overlap): in review, or approved and not cancelled / ended early.</summary>
    public bool Holds() => IsInReview() || IsApproved();
}

public static class VisitEvidenceExceptionStatuses
{
    public const string Draft = "draft";
    public const string InReview = "in_review";
    public const string Approved = "approved";
    public const string Cancelled = "cancelled";
    public const string EndedEarly = "ended_early";

    public static readonly IReadOnlyList<string> All = new[] { Draft, InReview, Approved, Cancelled, EndedEarly };
}

public static class VisitEvidenceExceptionPhases
{
    public const string Planned = "planned";
    public const string Active = "active";
    public const string Expired = "expired";

    public static readonly IReadOnlyList<string> All = new[] { Planned, Active, Expired };
}

/// <summary>The three rules an exception may relax (and the policy field each one relaxes).</summary>
public static class VisitEvidenceExceptionRules
{
    /// <summary>locationMode → <c>info</c> (a far / missing / inaccurate fix starts with a flag).</summary>
    public const string LocationInfo = "location_info";

    /// <summary>photoMode → <c>optional</c>.</summary>
    public const string PhotoOptional = "photo_optional";

    /// <summary>accountWithoutLocation → <c>allow_flagged</c>.</summary>
    public const string AccountWithoutLocationAllow = "account_without_location_allow";

    /// <summary>WP-VW-W3E (W3E-BE-a, K-W3E-9) — qrMode <c>required</c> → <c>optional</c>.</summary>
    public const string QrOptional = "qr_optional";

    public static readonly IReadOnlyList<string> All = new[] { LocationInfo, PhotoOptional, AccountWithoutLocationAllow, QrOptional };
}

public static class VisitEvidenceExceptionLimits
{
    public const int MaxDays = 30;
    public const int MaxReasonLength = 500;
}

/// <summary>The coded failures of the exceptions (<c>[message, code]</c>).</summary>
public static class VisitEvidenceExceptionErrors
{
    public const string Invalid = "visit_evidence_exception_invalid";
    public const string Overlap = "visit_evidence_exception_overlap";
    public const string NotFound = "visit_evidence_exception_not_found";
    public const string NotEditable = "visit_evidence_exception_not_editable";
    public const string SeparationOfDuties = "visit_evidence_exception_sod";
    public const string RepUnknown = "visit_evidence_exception_rep_unknown";
}

/// <summary>
/// WP-VW-W3 (W3-BE-c, §2.2) — one APPEND-ONLY row of the evidence policy history: a field changed by a PUT (or the
/// policy document created). Never updated, never deleted.
/// </summary>
public sealed class VisitEvidencePolicyChange : EntityBase
{
    public DateTimeOffset At { get; set; }
    public string? By { get; set; }

    /// <summary><c>tenant</c> | <c>legal-entity</c> | <c>business-unit</c> (<see cref="VisitEvidencePolicyScopeTypes"/>).
    /// A row written before WP-VW-W3L is a tenant row.</summary>
    public string Scope { get; set; } = VisitEvidencePolicyScopes.Tenant;

    /// <summary>WP-VW-W3L — the layer's legal entity id / business unit code; null for the tenant.</summary>
    public string? ScopeRef { get; set; }

    /// <summary>The policy field (camelCase, as on the wire); null for the <c>created</c> row.</summary>
    public string? Field { get; set; }

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    /// <summary><c>set</c> | <c>created</c> | <c>cleared</c> (a layer stops overriding the field) | <c>archived</c>
    /// (the scope was removed).</summary>
    public string Kind { get; set; } = VisitEvidencePolicyChangeKinds.Set;
}

public static class VisitEvidencePolicyScopes
{
    public const string Tenant = VisitEvidencePolicyScopeTypes.Tenant;
}

public static class VisitEvidencePolicyChangeKinds
{
    public const string Set = "set";
    public const string Created = "created";

    /// <summary>WP-VW-W3L — a layer no longer overrides the field (it is inherited again).</summary>
    public const string Cleared = "cleared";

    /// <summary>WP-VW-W3L — "remove the scope": the layer is archived (K-L6).</summary>
    public const string Archived = "archived";
}
