using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Queries;

/// <summary>
/// MOD-0167 FU04 (WP-ST-SCOPE) — the cascading scope selector's single source. A READ: it decides nothing about what may
/// be SAVED, which stays the write path's vocabulary check, so a code missing from this list but present in the published
/// set is still accepted. A deliberate mirror of <c>GetCampaignScopeOptionsQuery</c>.
/// </summary>
/// <param name="Country">
/// The country the author is filtering business units by. Business-unit candidates come from the territory plans covering
/// a country, so the country is the reason a given unit is offered.
/// </param>
public sealed record GetStrategyTemplateScopeOptionsQuery(
    string? Country,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate) : IRequest<Response<StrategyTemplateScopeOptionsDto>>;
