namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// WP-KP-5a (DESIGN-KP-STUDIO §2.4 / §4 D-KP-5 / §9.1) — the shared lifecycle of the two Regulatory-approved master texts
/// the page designer's locked blocks read: <see cref="SafetyText"/> (product × country × language) and
/// <see cref="CountryLegalProfile"/> (country × language). Not a claim (K2): nothing here touches the claim model.
/// <list type="bullet">
/// <item><c>draft</c> → submit → <c>in-review</c> (one MOD-0023 round, template <c>KP-REG-{CC}</c>) →
/// approved → <c>active</c> (the previous active of the key → <c>superseded</c>); rejected / withdrawn / cancelled →
/// back to <c>draft</c> with the decision kept; <c>archived</c> is the soft end (no delete).</item>
/// <item>At most ONE <c>active</c> and ONE open (draft / in-review) record per key; a new version is a clone of the
/// active (or a superseded) one.</item>
/// <item>Only a <c>draft</c> is editable; from <c>in-review</c> on the content is frozen.</item>
/// </list>
/// <see cref="EntityBase.Version"/> stays the concurrency token; the business version is <see cref="VersionNumber"/>.
/// </summary>
public abstract class RegulatoryText : EntityBase
{
    /// <summary>Server-generated, shared by every version of one key (e.g. <c>SAF-TR-0001</c>).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Upper-case ISO country (a <c>COUNTRY_CODES</c> value).</summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>Lower-case language — one of the country's <c>country-content-languages</c>.</summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>Business version of the key, from 1.</summary>
    public int VersionNumber { get; set; } = 1;

    public string Status { get; set; } = RegulatoryTextStatuses.Draft;

    /// <summary>The MOD-0023 rounds (the latest one is the current); the claim review round shape is reused.</summary>
    public List<ClaimReviewRound> ReviewRounds { get; set; } = new();

    /// <summary>The decisions taken on the rounds (who, approve / reject, comment, when) — kept after a rejection.</summary>
    public List<RegulatoryTextDecision> Decisions { get; set; } = new();

    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? SupersededAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    /// <summary>The key a single active / single open version is counted on.</summary>
    public abstract string Key();

    /// <summary>
    /// The key while this version is OPEN (draft / in review), otherwise null — DERIVED, never authored. It is stored
    /// only so the database can hold "at most one open version per key" with a unique index whose partial filter is
    /// <c>{ OpenKey: { $type: "string" } }</c> (no <c>$ne</c> / <c>$in</c>, which a partial filter must not rely on).
    /// The setter ignores the stored value on read: the state of the text is the only source.
    /// </summary>
    public string? OpenKey
    {
        get => IsOpenVersion() ? Key() : null;
        set { }
    }

    public bool IsArchived() => ArchivedAt is not null
                                || string.Equals(Status, RegulatoryTextStatuses.Archived, StringComparison.Ordinal);

    public bool IsDraft() => string.Equals(Status, RegulatoryTextStatuses.Draft, StringComparison.Ordinal);

    public bool IsInReview() => string.Equals(Status, RegulatoryTextStatuses.InReview, StringComparison.Ordinal);

    public bool IsActive() => string.Equals(Status, RegulatoryTextStatuses.Active, StringComparison.Ordinal);

    public bool IsSuperseded() => string.Equals(Status, RegulatoryTextStatuses.Superseded, StringComparison.Ordinal);

    /// <summary>WP-KP-5a-FIX-1 — the archive rule in ONE place (the archive command and the read's <c>canArchive</c>): a
    /// text can be archived unless it is already archived or in review (a review is withdrawn first).</summary>
    public bool IsArchivable() => !IsArchived() && !IsInReview();

    /// <summary>Draft or in review — the "open" version of a key.</summary>
    public bool IsOpenVersion() => !IsArchived() && (IsDraft() || IsInReview());

    public ClaimReviewRound? CurrentRound() => ReviewRounds.OrderBy(r => r.RoundNo).LastOrDefault();

    public ClaimReviewRound? OpenRound() => CurrentRound() is { } round && round.IsOpen() ? round : null;
}

/// <summary>WP-KP-5a — one decision on a review round (CRM decision endpoint, or the MOD-0023 outcome when it was taken
/// in the Work Center — then without a comment).</summary>
public sealed class RegulatoryTextDecision
{
    public int RoundNo { get; set; }
    public string? By { get; set; }

    /// <summary><c>approve</c> | <c>reject</c> (a cancelled / timed-out round leaves no decision).</summary>
    public string Outcome { get; set; } = string.Empty;

    public string? Comment { get; set; }
    public DateTimeOffset At { get; set; }
}

public static class RegulatoryTextStatuses
{
    public const string Draft = "draft";
    public const string InReview = "in-review";
    public const string Active = "active";
    public const string Superseded = "superseded";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Draft, InReview, Active, Superseded, Archived };

    public static bool IsValid(string? value) => value is not null && All.Contains(value.Trim().ToLowerInvariant());
}

public static class RegulatoryTextDecisionOutcomes
{
    public const string Approve = "approve";
    public const string Reject = "reject";
}

/// <summary>WP-KP-5a — the coded refusals of the two regulatory texts (rendered as <c>[code, message]</c>).</summary>
public static class RegulatoryTextErrors
{
    public const string SafetyTextOpenDraftExists = "safety_text_open_draft_exists";
    public const string LegalProfileOpenDraftExists = "legal_profile_open_draft_exists";
    public const string SafetyTextMissing = "safety_text_missing";
    public const string LegalProfileMissing = "legal_profile_missing";
    public const string ReviewTemplateMissing = "review_template_missing";
    public const string RejectionCommentRequired = "rejection_comment_required";
    public const string NotEditable = "not_editable";
    public const string SelfDecisionForbidden = "self_decision_forbidden";
    public const string ProductNotFound = "product_not_found";
    public const string DependencyUnavailable = "dependency_unavailable";
    public const string TextRequired = "text_required";
    public const string TextTooLong = "text_too_long";
    public const string DecisionInvalid = "decision_invalid";
    public const string NewVersionSourceInvalid = "new_version_source_invalid";
}

/// <summary>WP-KP-5a — text length limits (characters).</summary>
public static class RegulatoryTextLimits
{
    public const int SafetyBody = 20_000;
    public const int SafetyShortBody = 2_000;
    public const int Reference = 200;
    public const int LegalText = 10_000;
    public const int PageApprovalCodeFormat = 100;
    public const int Comment = 2_000;
}

/// <summary>
/// WP-KP-5a — the approved safety information of ONE product in ONE country and language (long + optional short text).
/// Key = (<see cref="GlobalProductId"/>, country, language). Plain text, paragraphs kept as written.
/// </summary>
public sealed class SafetyText : RegulatoryText
{
    /// <summary>MDM global product (validated fail-closed on write).</summary>
    public Guid GlobalProductId { get; set; }

    public string? GlobalProductCodeDisplay { get; set; }

    public string Body { get; set; } = string.Empty;

    /// <summary>Optional short form for narrow layouts.</summary>
    public string? ShortBody { get; set; }

    /// <summary>The source document (e.g. SmPC / KÜB and its revision).</summary>
    public string? SourceDocumentRef { get; set; }

    public DateTimeOffset? SourceDate { get; set; }

    /// <summary>The local approval number (free text).</summary>
    public string? ApprovalReference { get; set; }

    public override string Key() => $"{GlobalProductId:D}|{CountryCode}|{LanguageCode}";
}

/// <summary>
/// WP-KP-5a — the legal page furniture of ONE country and language: legal footer, marketing authorisation holder, adverse
/// event reporting text, promotional notice and the page approval code format (KP-UI-3). Key = (country, language).
/// </summary>
public sealed class CountryLegalProfile : RegulatoryText
{
    public string LegalFooterText { get; set; } = string.Empty;

    /// <summary>Name + address of the marketing authorisation holder.</summary>
    public string? MarketingAuthorizationHolder { get; set; }

    public string? AdverseEventReportingText { get; set; }

    public string? PromotionalNotice { get; set; }

    /// <summary>e.g. <c>{CC}-{YYYY}-{SEQ}</c>.</summary>
    public string? PageApprovalCodeFormat { get; set; }

    public override string Key() => $"{CountryCode}|{LanguageCode}";
}
