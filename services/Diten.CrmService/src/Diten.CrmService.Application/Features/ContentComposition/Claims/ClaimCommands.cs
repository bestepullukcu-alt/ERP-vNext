using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

/// <summary>SCMM-12 (CAND-CAP-0011) — claim applicability input: product / market / audience references (config strings,
/// sector-neutral) + an optional eligibility-policy reference (by id). All refs are opaque provenance in this slice.</summary>
public sealed record ClaimApplicabilityInput(
    IReadOnlyList<string>? ProductRefs = null,
    IReadOnlyList<string>? MarketRefs = null,
    IReadOnlyList<string>? AudienceRefs = null,
    Guid? EligibilityPolicyId = null);

/// <summary>SCMM-12 (CAND-CAP-0011) claim write surface. <c>TenantId</c> server-resolved. No delete — closing is
/// <see cref="ArchiveClaimCommand"/>. Approval is the outcome of a MOD-0023 workflow round (WP-CL-BE-4, draft → in-review → approved);
/// an approved claim freezes its governed body (change ⇒ new version). <c>EvidenceRefs</c> are opaque (MOD-0031
/// resolution deferred); <c>ComponentRefs</c> are by-id KnowledgeContent references (D02c reuse, provenance only).
/// <para>WP-CL-BE-1 (claims v2) — the trailing members are optional. When <c>Kind</c> is given (a v2 client) the MDM
/// product is required and proven fail-closed; <c>local</c> needs <c>LocalCountryCode</c> ∈ <c>COUNTRY_CODES</c>.</para></summary>
public sealed record CreateClaimCommand(
    string ClaimCode,
    string ClaimName,
    string ClaimText,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    IReadOnlyList<string>? Qualifiers = null,
    ClaimApplicabilityInput? Applicability = null,
    IReadOnlyList<string>? EvidenceRefs = null,
    IReadOnlyList<Guid>? ComponentRefs = null,
    string? ClaimVersion = null,
    string? Status = null,
    DateTimeOffset? EffectiveTo = null,
    string? Kind = null,
    string? LocalCountryCode = null,
    Guid? ProductId = null,
    string? ProductDisplay = null,
    IReadOnlyList<Guid>? AudienceProfileIds = null,
    Guid? ResponsibleOrgUnitId = null,
    string? TextLanguageCode = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields. <c>ClaimCode</c> is immutable. An archived claim cannot be updated; an
/// APPROVED claim freezes its governed body (change ⇒ new version). Approval / archive go through their own commands.
/// <para>WP-CL-BE-1 — the trailing v2 members are optional: <c>null</c> keeps the stored value (so a pre-v2 client
/// never wipes them). <c>Kind</c> / <c>LocalCountryCode</c> are immutable after create.</para></summary>
public sealed record UpdateClaimCommand(
    Guid ClaimId,
    string ClaimName,
    string ClaimText,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    IReadOnlyList<string>? Qualifiers = null,
    ClaimApplicabilityInput? Applicability = null,
    IReadOnlyList<string>? EvidenceRefs = null,
    IReadOnlyList<Guid>? ComponentRefs = null,
    string? ClaimVersion = null,
    string? Status = null,
    DateTimeOffset? EffectiveTo = null,
    Guid? ProductId = null,
    string? ProductDisplay = null,
    IReadOnlyList<Guid>? AudienceProfileIds = null,
    Guid? ResponsibleOrgUnitId = null,
    string? TextLanguageCode = null) : IRequest<Response<bool>>;

public sealed record ArchiveClaimCommand(Guid ClaimId) : IRequest<Response<bool>>;

// ---------------------------------------------------------------- WP-CL-BE-1 (claims v2)

/// <summary>Opens the next core major ("1.0" → "2.0") from an APPROVED claim as a new draft record of the same
/// ClaimCode. At most one draft / in-review record per code.</summary>
public sealed record CreateClaimNewVersionCommand(Guid ClaimId) : IRequest<Response<Guid>>;

/// <summary>Closes a claim × country cell with a single-choice reason (<c>claim-country-closure-reason</c>).</summary>
public sealed record CloseClaimCountryCommand(Guid ClaimId, string CountryCode, string ReasonCode)
    : IRequest<Response<bool>>;

/// <summary>Reopens a closed claim × country cell (the closure entry is stamped, never removed).</summary>
public sealed record ReopenClaimCountryCommand(Guid ClaimId, string CountryCode, string? Note = null)
    : IRequest<Response<bool>>;

public sealed record ClaimLocalizedTextInput(string LanguageCode, string Text);

/// <summary>Opens the first country version of a claim for one country.</summary>
public sealed record CreateClaimCountryVersionCommand(
    Guid ClaimId,
    string CountryCode,
    IReadOnlyList<ClaimLocalizedTextInput>? Texts,
    IReadOnlyList<ClaimLocalizedTextInput>? Qualifiers,
    string? AdaptationTypeCode,
    string? AdaptationReason,
    IReadOnlyList<Guid>? AudienceProfileIds,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of a DRAFT country version's content (an approved version is locked).</summary>
public sealed record UpdateClaimCountryVersionCommand(
    Guid CountryVersionId,
    IReadOnlyList<ClaimLocalizedTextInput>? Texts,
    IReadOnlyList<ClaimLocalizedTextInput>? Qualifiers,
    string? AdaptationTypeCode,
    string? AdaptationReason,
    IReadOnlyList<Guid>? AudienceProfileIds,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo = null) : IRequest<Response<bool>>;

/// <summary>Opens the next country minor ("1.0" → "1.1") from an approved / review-required version, bound to the
/// latest approved core.</summary>
public sealed record CreateClaimCountryNewVersionCommand(Guid CountryVersionId) : IRequest<Response<Guid>>;

public sealed record ArchiveClaimCountryVersionCommand(Guid CountryVersionId) : IRequest<Response<bool>>;

// ---------------------------------------------------------------- WP-CL-BE-4 (approval via MOD-0023 workflow)
// Approval is ONLY the outcome of a MOD-0023 workflow round (ClaimReviewOutcomeApplier). There is no direct approve.

/// <summary>Sends a DRAFT core/local claim to its MOD-0023 approval workflow, as the calling user (SoD).</summary>
public sealed record SubmitClaimReviewCommand(Guid ClaimId) : IRequest<Response<ClaimReviewRoundDto>>;

/// <summary>Sends a DRAFT country version to the country's MOD-0023 approval workflow, as the calling user.</summary>
public sealed record SubmitClaimCountryVersionReviewCommand(Guid CountryVersionId) : IRequest<Response<ClaimReviewRoundDto>>;

/// <summary>Withdraws an in-review claim (cancels the open workflow task); the record returns to draft.</summary>
public sealed record WithdrawClaimReviewCommand(Guid ClaimId) : IRequest<Response<ClaimReviewRoundDto>>;

/// <summary>Withdraws an in-review country version; the version returns to draft.</summary>
public sealed record WithdrawClaimCountryVersionReviewCommand(Guid CountryVersionId) : IRequest<Response<ClaimReviewRoundDto>>;
