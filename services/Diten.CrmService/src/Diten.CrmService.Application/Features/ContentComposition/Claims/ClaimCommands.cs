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
/// <see cref="ArchiveClaimCommand"/>. Approval is the dedicated <see cref="ApproveClaimCommand"/> (draft → approved);
/// an approved claim freezes its governed body (change ⇒ new version). <c>EvidenceRefs</c> are opaque (MOD-0031
/// resolution deferred); <c>ComponentRefs</c> are by-id KnowledgeContent references (D02c reuse, provenance only).</summary>
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
    DateTimeOffset? EffectiveTo = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields. <c>ClaimCode</c> is immutable. An archived claim cannot be updated; an
/// APPROVED claim freezes its governed body (change ⇒ new version). Approval / archive go through their own commands.</summary>
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
    DateTimeOffset? EffectiveTo = null) : IRequest<Response<bool>>;

/// <summary>Approves a draft claim (draft → approved). Claim approval is NOT assembly approval (that is SCMM-15/17).</summary>
public sealed record ApproveClaimCommand(Guid ClaimId) : IRequest<Response<bool>>;

public sealed record ArchiveClaimCommand(Guid ClaimId) : IRequest<Response<bool>>;
