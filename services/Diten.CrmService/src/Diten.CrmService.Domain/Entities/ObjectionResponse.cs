namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// WP-VW-W3 (W3-BE-b, §2.3) — one approved answer to a doctor's objection, kept in a tenant library the rep reaches from
/// the presentation ("Notlar → İtirazlar"). A product (MDM Global Product id), a category (reference set
/// <c>visit-objection-category</c>), the question and the answer, and the APPROVED claims the answer rests on.
/// <para><b>Lifecycle:</b> <c>draft</c> → <c>approved</c> → <c>retired</c>. Only a draft is edited; the approver is never
/// its author (SoD); an approved record never changes — a change is a retire + a new record. Only an approved record is
/// ever shown to a rep. No competitor card, no market share (K-W6 / W7).</para>
/// </summary>
public sealed class ObjectionResponse : EntityBase
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

    public bool IsDraft() => string.Equals(Status, ObjectionResponseStatuses.Draft, StringComparison.Ordinal);
    public bool IsApproved() => string.Equals(Status, ObjectionResponseStatuses.Approved, StringComparison.Ordinal);
    public bool IsRetired() => string.Equals(Status, ObjectionResponseStatuses.Retired, StringComparison.Ordinal);
}

public static class ObjectionResponseStatuses
{
    public const string Draft = "draft";
    public const string Approved = "approved";
    public const string Retired = "retired";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Approved, Retired };

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
    public const string ReferenceDataUnavailable = "reference_data_unavailable";
}
