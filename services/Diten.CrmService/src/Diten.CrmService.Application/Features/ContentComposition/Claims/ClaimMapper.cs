using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

/// <summary>SCMM-12 (CAND-CAP-0011) aggregate ↔ DTO projection. Reads never echo TenantId (server-resolved).</summary>
public static class ClaimMapper
{
    public static ClaimDto ToDto(Claim c) => new(
        c.Id,
        c.ClaimCode,
        c.ClaimName,
        c.Description,
        c.ClaimText,
        c.Qualifiers.ToList(),
        new ClaimApplicabilityDto(
            c.Applicability.ProductRefs.ToList(),
            c.Applicability.MarketRefs.ToList(),
            c.Applicability.AudienceRefs.ToList(),
            c.Applicability.EligibilityPolicyId),
        c.EvidenceRefs.ToList(),
        c.ComponentRefs.ToList(),
        c.ClaimVersion,
        c.Status,
        c.EffectiveFrom,
        c.EffectiveTo,
        c.ApprovedAt,
        c.ApprovedBy,
        c.CreatedAt,
        c.CreatedBy,
        c.UpdatedAt,
        c.UpdatedBy,
        c.ArchivedAt,
        c.ArchivedBy,
        c.IsArchived());
}
