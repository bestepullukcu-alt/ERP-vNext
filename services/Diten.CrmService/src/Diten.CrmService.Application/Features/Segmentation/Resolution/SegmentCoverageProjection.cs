namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>One current-coverage fact for an account, flattened for segment evaluation. Produced from the MOD-0151
/// resolver output as-is: no coverage is invented, and no default is substituted when a tenant has no active model.</summary>
public sealed record SegmentCoverageProjection(
    Guid AccountId,
    Guid TerritoryNodeId,
    Guid TerritoryModelId);
