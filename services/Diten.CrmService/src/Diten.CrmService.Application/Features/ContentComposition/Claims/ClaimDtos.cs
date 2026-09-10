namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

/// <summary>SCMM-12 read model for claim applicability.</summary>
public sealed record ClaimApplicabilityDto(
    IReadOnlyList<string> ProductRefs,
    IReadOnlyList<string> MarketRefs,
    IReadOnlyList<string> AudienceRefs,
    Guid? EligibilityPolicyId);

/// <summary>SCMM-12 (CAND-CAP-0011) read model for a claim. The governed body is frozen once approved. EvidenceRefs are
/// opaque (MOD-0031 resolution deferred); ComponentRefs are by-id KnowledgeContent references (D02c reuse).</summary>
public sealed record ClaimDto(
    Guid ClaimId,
    string ClaimCode,
    string ClaimName,
    string? Description,
    string ClaimText,
    IReadOnlyList<string> Qualifiers,
    ClaimApplicabilityDto Applicability,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<Guid> ComponentRefs,
    string ClaimVersion,
    string Status,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

public sealed record ClaimListDto(IReadOnlyList<ClaimDto> Items, int Total);
