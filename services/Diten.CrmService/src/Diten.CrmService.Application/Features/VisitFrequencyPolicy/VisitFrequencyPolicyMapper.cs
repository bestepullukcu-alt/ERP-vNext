using Vfp = Diten.CrmService.Domain.Entities.VisitFrequencyPolicy;

namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy;

/// <summary>Aggregate → DTO projection for MOD-0165 FU03 reads.</summary>
public static class VisitFrequencyPolicyMapper
{
    public static VisitFrequencyPolicyDto ToDto(Vfp policy) => new(
        policy.Id,
        policy.PolicyCode,
        policy.PolicyName,
        policy.Description,
        policy.TargetType,
        policy.TargetId,
        policy.BusinessUnit,
        policy.TerritoryNodeId,
        policy.CampaignId,
        policy.SegmentId,
        policy.BrandId,
        policy.ProductId,
        policy.CycleId,
        policy.CyclePeriodId,
        policy.FrequencyType,
        policy.RequiredVisitCount,
        policy.PeriodType,
        policy.EffectiveFrom,
        policy.EffectiveTo,
        policy.Priority,
        policy.Source,
        policy.Status,
        policy.Notes,
        policy.CreatedAt,
        policy.CreatedBy,
        policy.UpdatedAt,
        policy.UpdatedBy,
        policy.ArchivedAt,
        policy.ArchivedBy);
}
