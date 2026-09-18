using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy.Commands;

/// <summary>
/// MOD-0165 FU03 write surface. TenantId is NEVER accepted from the payload (server-resolved from the JWT claim).
/// Closing a policy as history is <see cref="ArchiveVisitFrequencyPolicyCommand"/> (soft lifecycle, still listed);
/// removing it from the working set is <see cref="DeleteVisitFrequencyPolicyCommand"/> (WP-FREQ-A additive soft-delete —
/// NOT a hard delete). PolicyCode is stable; renaming is done through PolicyName on update.
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

/// <summary>Soft-deletes a policy (WP-FREQ-A): IsDeleted=true + DeletedAt/By stamped, so it leaves the list and the
/// resolve working set. DISTINCT from <see cref="ArchiveVisitFrequencyPolicyCommand"/> — archive keeps the row as
/// readable history (status=archived, still listed); delete removes it from the working set. Additive; there is no
/// hard delete and the FU03 contract/resolve/CRUD/archive behaviour is unchanged.</summary>
public sealed record DeleteVisitFrequencyPolicyCommand(Guid PolicyId) : IRequest<Response<bool>>;
