namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// WP-VW-W3 (W3-BE-b, §2.3) — one approved answer to a doctor's objection, kept in a tenant library the rep reaches from
/// the presentation ("Notlar → İtirazlar"). A product (MDM Global Product id), a category (reference set
/// <c>visit-objection-category</c>), the question and the answer, and the APPROVED claims the answer rests on.
/// <para><b>Lifecycle:</b> <c>draft</c> → <c>approved</c> → <c>retired</c>. Only a draft is edited; the approver is never
/// its author (SoD); an approved record never changes — a change is a retire + a new record. Only an approved record is
/// ever shown to a rep. No competitor card, no market share (K-W6 / W7).</para>
/// <para>WP-VW-W3 (W3-BE-c, §2.1) — approval moved to MOD-0023 + the Work Center: <c>draft</c> → <c>in_review</c> (edits
/// locked) → <c>approved</c>; a returned / cancelled / timed-out round goes back to <c>draft</c> keeping its decision. A
/// draft may be saved half-written (K-A3); completeness is checked on submit.</para>
/// </summary>
public sealed class ObjectionResponse : EntityBase, IWorkflowApprovable
{
    /// <summary>The MDM Global Product the objection is about.</summary>
    public Guid ProductId { get; set; }

    /// <summary>A <c>visit-objection-category</c> value code (reference data; no label in code).</summary>
    public string CategoryCode { get; set; } = string.Empty;

    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;

    /// <summary>The claims the answer rests on — approved claims only (checked on write and on approval).</summary>
    public List<Guid> ClaimIds { get; set; } = new();

    public string Status { get; set; } = ObjectionResponseStatuses.Draft;

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
    public string? RetiredBy { get; set; }

    /// <summary>WP-VW-W3 (W3-BE-c) — the MOD-0023 rounds (one open at most) and the decisions taken on them.</summary>
    public List<ClaimReviewRound> ReviewRounds { get; set; } = new();
    public List<RegulatoryTextDecision> Decisions { get; set; } = new();

    /// <summary>WP-VW-W3 (W3-BE-c) — the record this draft was copied from (POST {id}/copy), else null.</summary>
    public Guid? CopiedFromId { get; set; }

    public bool IsInReview() => string.Equals(Status, ObjectionResponseStatuses.InReview, StringComparison.Ordinal);

    public bool IsDraft() => string.Equals(Status, ObjectionResponseStatuses.Draft, StringComparison.Ordinal);
    public bool IsApproved() => string.Equals(Status, ObjectionResponseStatuses.Approved, StringComparison.Ordinal);
    public bool IsRetired() => string.Equals(Status, ObjectionResponseStatuses.Retired, StringComparison.Ordinal);
}

public static class ObjectionResponseStatuses
{
    public const string Draft = "draft";
    public const string InReview = "in_review";
    public const string Approved = "approved";
    public const string Retired = "retired";

    public static readonly IReadOnlyList<string> All = new[] { Draft, InReview, Approved, Retired };

    public static bool IsKnown(string? value) => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);
}

public static class ObjectionResponseLimits
{
    public const int MaxQuestionLength = 500;
    public const int MaxAnswerLength = 4000;
    public const int MaxClaims = 10;
}

/// <summary>The coded failures of the objection library (<c>[code, message]</c>).</summary>
public static class ObjectionResponseErrors
{
    public const string Invalid = "objection_response_invalid";
    public const string CategoryInvalid = "objection_category_invalid";
    public const string ProductInvalid = "objection_product_invalid";
    public const string ClaimNotApproved = "objection_claim_not_approved";
    public const string NotEditable = "objection_response_not_editable";
    public const string NotDraft = "objection_response_not_draft";
    public const string SeparationOfDuties = "objection_response_sod";
    public const string NotFound = "objection_response_not_found";

    // WP-VW-W3 (W3-BE-c, §2.1)
    /// <summary>409 — the old direct approve: an answer is approved only through its MOD-0023 workflow.</summary>
    public const string ApprovalViaWorkflowOnly = "approval_via_workflow_only";
    /// <summary>400 — submit: the question / answer / category is empty, or a claim is not this product's.</summary>
    public const string Incomplete = "objection_response_incomplete";
    /// <summary>409 — an answer under review is locked.</summary>
    public const string InReviewLocked = "objection_response_in_review";
    public const string ReferenceDataUnavailable = "reference_data_unavailable";
}
