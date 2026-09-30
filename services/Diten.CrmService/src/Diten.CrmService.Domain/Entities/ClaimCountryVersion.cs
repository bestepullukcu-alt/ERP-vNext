namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// WP-CL-BE-1 (claims v2, CAND-CAP-0011) — a claim's localisation for ONE country: per-language wording, adaptation
/// (verbatim / narrowed / softened + reason), an audience that may only narrow the core's, and a validity window.
/// <para>
/// <see cref="ClaimCode"/> ties the line to the logical claim across core versions; <see cref="ClaimId"/> is the claim
/// RECORD (core or local) this version is bound to and <see cref="BoundCoreVersion"/> that record's business version.
/// <see cref="Version"/> is the country minor line ("1.0" → "1.1"), never <see cref="EntityBase.Version"/> (the
/// concurrency token) — hence the <c>CountryVersion</c> property name.
/// </para>
/// <para>
/// Country and language lists are NEVER stored in code: the country axis is the <c>COUNTRY_CODES</c> reference set and
/// the allowed languages the <c>country-content-languages</c> set's <c>Languages</c> attribute. An approved version is
/// locked; a change is a new country version. <see cref="ReviewRounds"/> records its MOD-0023 approval rounds (WP-CL-BE-4).
/// </para>
/// </summary>
public sealed class ClaimCountryVersion : EntityBase
{
    public string ClaimCode { get; set; } = string.Empty;

    /// <summary>The bound claim record (core, or the local claim itself).</summary>
    public Guid ClaimId { get; set; }

    /// <summary>Business version of the bound claim record at binding time (e.g. "1.0").</summary>
    public string BoundCoreVersion { get; set; } = string.Empty;

    /// <summary>A <c>COUNTRY_CODES</c> value (upper case).</summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>Country business version ("1.0", "1.1" …).</summary>
    public string CountryVersion { get; set; } = "1.0";

    public List<ClaimLocalizedText> Texts { get; set; } = new();
    public List<ClaimLocalizedText> Qualifiers { get; set; } = new();

    /// <summary>A <c>claim-adaptation-type</c> value.</summary>
    public string AdaptationTypeCode { get; set; } = string.Empty;

    /// <summary>Required unless the adaptation is <c>verbatim</c>.</summary>
    public string? AdaptationReason { get; set; }

    /// <summary>⊆ the bound claim's <see cref="Claim.AudienceProfileIds"/> (free when the claim has none).</summary>
    public List<Guid> AudienceProfileIds { get; set; } = new();

    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }

    /// <summary><see cref="ClaimStatuses"/> — draft / in-review / approved / review-required / inactive / archived.</summary>
    public string Status { get; set; } = ClaimStatuses.Draft;

    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }

    /// <summary>The approved / review-required version this one was opened from.</summary>
    public Guid? SupersedesVersionId { get; set; }

    /// <summary>MOD-0023 approval rounds of this version (WP-CL-BE-4), newest last; at most one open.</summary>
    public List<ClaimReviewRound> ReviewRounds { get; set; } = new();

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;

    /// <summary>Live = part of the country's current line (not archived, not superseded-inactive).</summary>
    public bool IsLive()
        => !IsArchived() && Status is not (ClaimStatuses.Inactive or ClaimStatuses.Archived);

    /// <summary>An open edit/approval cycle (at most one per claim code × country).</summary>
    public bool IsOpen() => Status is ClaimStatuses.Draft or ClaimStatuses.InReview;
}

/// <summary>WP-CL-BE-1 — a per-language text (wording or qualifier).</summary>
public sealed class ClaimLocalizedText
{
    public string LanguageCode { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

/// <summary>WP-CL-BE-1 — placeholder shape of one approval round (one workflow instance). Populated by WP-CL-BE-4.</summary>
public sealed class ClaimReviewRound
{
    public Guid WorkflowInstanceId { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public string? SubmittedBy { get; set; }

    /// <summary><see cref="ClaimReviewOutcomes"/>; null while the round is open.</summary>
    public string? Outcome { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    // WP-CL-BE-4 — 1-based round number (also part of the workflow idempotency key), the MOD-0023 template the round
    // ran on, who closed it (principal id from the workflow; null for a system timeout) and the closing reason code.
    public int RoundNo { get; set; }
    public string? TemplateCode { get; set; }
    public string? CompletedBy { get; set; }
    public string? ReasonCode { get; set; }

    public bool IsOpen() => Outcome is null && ClosedAt is null;
}

/// <summary>WP-CL-BE-4 — how a review round closed (the MOD-0023 completion outcomes).</summary>
public static class ClaimReviewOutcomes
{
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";
    public const string TimedOut = "timed-out";

    public static bool IsValid(string? value) => value is Approved or Rejected or Cancelled or TimedOut;
}
