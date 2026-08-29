using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Queries;

/// <summary>
/// The read-only binding view with derived freshness hints (a bound segment superseded, a bound content archived, a
/// referenced policy no longer active). The hints are WARNINGS, never blocks: an active play does not become invalid
/// because something it points at moved on, and the past must stay explainable.
/// <para>It returns no member, no member count and no subject id: reading a play never implies the right to see the
/// people inside its segments.</para>
/// </summary>
public sealed record GetStrategyTemplateBindingsQuery(Guid TemplateId, DateTimeOffset? EffectiveAt)
    : IRequest<Response<StrategyTemplateBindingsDto>>;
