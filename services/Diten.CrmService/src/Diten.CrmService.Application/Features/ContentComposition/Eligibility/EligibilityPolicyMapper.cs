using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.ContentComposition.Eligibility;

/// <summary>SCMM-11 (CAND-CAP-0011) aggregate ↔ DTO projection. Reads never echo TenantId (server-resolved).</summary>
public static class EligibilityPolicyMapper
{
    public static EligibilityPolicyDto ToDto(EligibilityPolicy p) => new(
        p.Id,
        p.PolicyCode,
        p.PolicyName,
        p.Description,
        p.PolicyVersion,
        p.Status,
        p.Conditions.Select(c => new EligibilityConditionDto(
            c.Dimension, c.Match, c.Values.ToList(), c.Required)).ToList(),
        p.EffectiveFrom,
        p.EffectiveTo,
        p.CreatedAt,
        p.CreatedBy,
        p.UpdatedAt,
        p.UpdatedBy,
        p.ArchivedAt,
        p.ArchivedBy,
        p.IsArchived());
}
