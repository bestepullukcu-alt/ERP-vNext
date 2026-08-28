namespace Diten.CrmService.Api.Models.CRM;

/// <summary>
/// MOD-0165 FU03 request bodies. Note what is NOT here: <c>TenantId</c> (server-resolved from the JWT claim). On
/// update, <c>PolicyCode</c> and <c>TargetType</c>/<c>TargetId</c> are also absent — they are immutable (a new target
/// is a new policy). There is no delete body: closing a policy is the archive endpoint.
/// </summary>
public sealed record CreateVisitFrequencyPolicyRequest(
    string PolicyCode,
    string PolicyName,
    string TargetType,
    Guid TargetId,
    string FrequencyType,
    int RequiredVisitCount,
    string PeriodType,
    DateTimeOffset EffectiveFrom,
    int Priority,
    string Source,
    string? Status = null,
    string? Description = null,
    string? BusinessUnit = null,
    Guid? TerritoryNodeId = null,
    Guid? CampaignId = null,
    Guid? SegmentId = null,
    Guid? BrandId = null,
    Guid? ProductId = null,
    Guid? CycleId = null,
    Guid? CyclePeriodId = null,
    DateTimeOffset? EffectiveTo = null,
    string? Notes = null);

public sealed record UpdateVisitFrequencyPolicyRequest(
    string PolicyName,
    string FrequencyType,
    int RequiredVisitCount,
    string PeriodType,
    DateTimeOffset EffectiveFrom,
    int Priority,
    string Source,
    string? Status = null,
    string? Description = null,
    string? BusinessUnit = null,
    Guid? TerritoryNodeId = null,
    Guid? CampaignId = null,
    Guid? SegmentId = null,
    Guid? BrandId = null,
    Guid? ProductId = null,
    Guid? CycleId = null,
    Guid? CyclePeriodId = null,
    DateTimeOffset? EffectiveTo = null,
    string? Notes = null);
