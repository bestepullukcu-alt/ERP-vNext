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
    DateTimeOffset? EffectiveTo = null);

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
    DateTimeOffset? EffectiveTo = null);
