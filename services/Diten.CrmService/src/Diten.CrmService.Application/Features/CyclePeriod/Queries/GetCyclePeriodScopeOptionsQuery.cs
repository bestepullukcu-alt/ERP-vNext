using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Queries;

/// <summary>
/// FU07 — everything the cascading scope selector needs, in one round trip: the scope levels, the governed country
/// values, the tenant's referenceable legal entities, and the business units the tenant's ACTIVE territory plans
/// actually cover for that country and that window.
/// <para>It is a READ and it decides nothing. The business-unit list NARROWS the picker; what may be WRITTEN is
/// decided by the published <c>business-unit</c> vocabulary in the write handler, so a period stays authorable before
/// its field plan exists.</para>
/// </summary>
public sealed record GetCyclePeriodScopeOptionsQuery(
    string? Country,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate) : IRequest<Response<CyclePeriodScopeOptionsDto>>;
