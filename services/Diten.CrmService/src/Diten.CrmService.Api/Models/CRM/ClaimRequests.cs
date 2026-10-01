namespace Diten.CrmService.Api.Models.CRM;

// SCMM-12-API (CAND-CAP-0011) claim request models. TenantId is NEVER part of any request body — it is server-resolved
// from the JWT claim. Route ids (claimId) come from the path, never the body. These map 1:1 onto the ready
// CreateClaimCommand / UpdateClaimCommand (approve / archive carry only the route id, so they need no body).

/// <summary>SCMM-12 claim applicability request — product / market / audience references (config strings) + optional
/// eligibility-policy reference. Opaque provenance (no resolution in this surface).</summary>
public sealed record ClaimApplicabilityRequest(
    IReadOnlyList<string>? ProductRefs = null,
    IReadOnlyList<string>? MarketRefs = null,
    IReadOnlyList<string>? AudienceRefs = null,
    Guid? EligibilityPolicyId = null);

// WP-CL-BE-1 (claims v2) — the trailing members of the create / update bodies are optional additions; a pre-v2 body
// binds exactly as before.
public sealed record CreateClaimRequest(
    string ClaimCode,
    string ClaimName,
    string ClaimText,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    IReadOnlyList<string>? Qualifiers = null,
    ClaimApplicabilityRequest? Applicability = null,
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
    string? TextLanguageCode = null);

public sealed record UpdateClaimRequest(
    string ClaimName,
    string ClaimText,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    IReadOnlyList<string>? Qualifiers = null,
    ClaimApplicabilityRequest? Applicability = null,
    IReadOnlyList<string>? EvidenceRefs = null,
    IReadOnlyList<Guid>? ComponentRefs = null,
    string? ClaimVersion = null,
    string? Status = null,
    DateTimeOffset? EffectiveTo = null,
    Guid? ProductId = null,
    string? ProductDisplay = null,
    IReadOnlyList<Guid>? AudienceProfileIds = null,
    Guid? ResponsibleOrgUnitId = null,
    string? TextLanguageCode = null);

/// <summary>WP-CL-BE-1 — close a claim × country cell with a single-choice reason (claim-country-closure-reason).</summary>
public sealed record CloseClaimCountryRequest(string CountryCode, string ReasonCode);

/// <summary>WP-CL-BE-1 — reopen a closed claim × country cell.</summary>
public sealed record ReopenClaimCountryRequest(string? Note = null);

public sealed record ClaimLocalizedTextRequest(string LanguageCode, string Text);

/// <summary>WP-CL-BE-1 — open a claim country version.</summary>
public sealed record CreateClaimCountryVersionRequest(
    string CountryCode,
    IReadOnlyList<ClaimLocalizedTextRequest>? Texts,
    DateTimeOffset ValidFrom,
    IReadOnlyList<ClaimLocalizedTextRequest>? Qualifiers = null,
    string? AdaptationTypeCode = null,
    string? AdaptationReason = null,
    IReadOnlyList<Guid>? AudienceProfileIds = null,
    DateTimeOffset? ValidTo = null);

/// <summary>WP-CL-BE-1 — full replace of a draft claim country version.</summary>
public sealed record UpdateClaimCountryVersionRequest(
    IReadOnlyList<ClaimLocalizedTextRequest>? Texts,
    DateTimeOffset ValidFrom,
    IReadOnlyList<ClaimLocalizedTextRequest>? Qualifiers = null,
    string? AdaptationTypeCode = null,
    string? AdaptationReason = null,
    IReadOnlyList<Guid>? AudienceProfileIds = null,
    DateTimeOffset? ValidTo = null);

// WP-CL-BE-5 — claim evidence (MOD-0031). There is deliberately NO objectRef in the body: CRM builds it from the
// addressed claim / country version.
public sealed record ClaimEvidenceLocatorRequest(string? Quote, string? Section = null, string? Page = null, string? Table = null);

public sealed record ClaimEvidenceSpanRequest(string LanguageCode, string Text, int? Start = null, int? End = null);

public sealed record LinkClaimEvidenceRequest(
    string? DocumentKind,
    Guid DocumentId,
    Guid? DocumentVersionId,
    string? EvidenceTypeCode,
    ClaimEvidenceLocatorRequest? Locator,
    IReadOnlyList<ClaimEvidenceSpanRequest>? SupportedSpans = null);

public sealed record RemoveClaimEvidenceRequest(string? Reason);
