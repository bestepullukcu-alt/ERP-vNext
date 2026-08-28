using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Campaign.Queries;

/// <summary>
/// MOD-0165 FU09 — the cascading scope selector's single source. A READ: it decides nothing about what may be SAVED,
/// which stays the write path's vocabulary check, so a code missing from this list but present in the published set is
/// still accepted.
/// </summary>
/// <param name="Country">
/// The country the author is filtering business units by. Business-unit candidates come from the territory plans
/// covering a country, so the country is the reason a given unit is offered.
/// </param>
public sealed record GetCampaignScopeOptionsQuery(
    string? Country,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate) : IRequest<Response<CampaignScopeOptionsDto>>;

/// <summary>
/// MOD-0165 FU09 — the cycle periods a campaign at a given scope may bind to.
/// <para>The scope is supplied by the caller rather than read from a stored campaign on purpose: the picker has to
/// answer for the scope the author is CURRENTLY editing, which may not be the one on disk yet.</para>
/// <para>Only ACTIVE periods are listed: a draft period's dates can still move, and a closed one cannot be newly
/// bound. That mirrors the FU08 bind rule, so the picker never offers something the write path would refuse.</para>
/// </summary>
public sealed record GetApplicableCyclePeriodsQuery(
    string? ScopeType,
    string? CountryScope,
    Guid? LegalEntityId,
    string? BusinessUnitId) : IRequest<Response<CampaignApplicableCyclePeriodsDto>>;
