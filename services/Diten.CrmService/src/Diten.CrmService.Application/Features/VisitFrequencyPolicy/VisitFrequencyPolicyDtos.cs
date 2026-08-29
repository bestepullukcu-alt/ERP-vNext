namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy;

/// <summary>MOD-0165 FU03 read model for a single policy (list + detail). Mirrors the aggregate; TenantId is never
/// echoed as it is server-resolved.</summary>
public sealed record VisitFrequencyPolicyDto(
    Guid PolicyId,
    string PolicyCode,
    string PolicyName,
    string? Description,
    string TargetType,
    Guid TargetId,
    string? BusinessUnit,
    Guid? TerritoryNodeId,
    Guid? CampaignId,
    Guid? SegmentId,
    Guid? BrandId,
    Guid? ProductId,
    Guid? CycleId,
    Guid? CyclePeriodId,
    string FrequencyType,
    int RequiredVisitCount,
    string PeriodType,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    int Priority,
    string Source,
    string Status,
    string? Notes,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy);

/// <summary>Paged list envelope.</summary>
public sealed record VisitFrequencyPolicyListDto(
    IReadOnlyList<VisitFrequencyPolicyDto> Items,
    int Total);
