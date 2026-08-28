using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Campaign.Queries;

/// <summary>
/// Lists campaigns for the tenant. Archived rows are included by default so history stays visible.
/// <para>FU10 — the brand / product / subject filters were removed along with the fields they filtered: nobody can
/// author those any more, and a filter over an unauthorable field advertises a capability that no longer exists. The
/// targeting-mode filter takes their place.</para>
/// </summary>
public sealed record ListCampaignsQuery(
    string? CampaignType = null,
    string? CampaignStatus = null,
    string? TargetingMode = null,
    bool IncludeArchived = true) : IRequest<Response<CampaignListDto>>;

public sealed record GetCampaignQuery(Guid CampaignId) : IRequest<Response<CampaignDto>>;

/// <summary>Lists the targets of one campaign. Archived and excluded rows are included by default — an excluded target
/// with its reason is the audit trail of why someone was left out.</summary>
public sealed record ListCampaignTargetsQuery(
    Guid CampaignId,
    string? TargetType = null,
    string? TargetStatus = null,
    string? TargetSource = null,
    Guid? SnapshotBatchId = null,
    bool IncludeArchived = true) : IRequest<Response<CampaignTargetListDto>>;

public sealed record GetCampaignTargetQuery(Guid CampaignId, Guid CampaignTargetId)
    : IRequest<Response<CampaignTargetDto>>;

/// <summary>
/// FU11 — what the next auto-assigned CampaignCode would be, for the create form's placeholder.
/// <para><b>Read-only and non-committing.</b> It reads the tenant/year counter instead of incrementing it, so opening
/// the create form a hundred times still consumes nothing and leaves no gaps. The answer is a hint: the field is
/// submitted EMPTY and the real code is assigned at save, which is also why a stale hint cannot cause a collision.</para>
/// <para>Returns no data (not an error) when the retry budget finds no free candidate — the form simply opens without
/// a placeholder rather than showing a code that would not be the one assigned.</para>
/// </summary>
public sealed record PeekNextCampaignCodeQuery() : IRequest<Response<CampaignCodePeek>>;
