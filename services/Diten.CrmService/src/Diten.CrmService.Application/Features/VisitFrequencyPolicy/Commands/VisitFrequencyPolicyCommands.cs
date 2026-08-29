using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy.Commands;

/// <summary>
/// MOD-0165 FU03 write surface. TenantId is NEVER accepted from the payload (server-resolved from the JWT claim).
/// There is deliberately NO delete command — closing a policy is <see cref="ArchiveVisitFrequencyPolicyCommand"/>
/// (soft lifecycle). PolicyCode is stable; renaming is done through PolicyName on update.
/// </summary>
public sealed record CreateVisitFrequencyPolicyCommand(
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
    string? Notes = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields of a policy. PolicyCode and TargetType/TargetId are immutable — a new
/// target is a new policy, not an edit of this one.</summary>
public sealed record UpdateVisitFrequencyPolicyCommand(
    Guid PolicyId,
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
    string? Notes = null) : IRequest<Response<bool>>;

/// <summary>Archives a policy (status → archived, ArchivedAt/By stamped). Removed from resolve; still readable.</summary>
public sealed record ArchiveVisitFrequencyPolicyCommand(Guid PolicyId) : IRequest<Response<bool>>;
