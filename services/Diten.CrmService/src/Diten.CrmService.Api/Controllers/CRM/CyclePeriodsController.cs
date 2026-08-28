using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.CyclePeriod.Commands;
using Diten.CrmService.Application.Features.CyclePeriod.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.CyclePeriod.CyclePeriodPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0165 FU06/FU07 — CyclePeriod: the tenant's named planning period master, at one of four scope levels.
/// <para>There is <b>no DELETE, no PATCH and no bulk-delete</b> anywhere in this controller (ending a period is
/// <c>close</c>), and deliberately <b>no /reopen, /apply, /generate, /reschedule and no /working-days</b> path:
/// re-opening a closed period would rewrite what a past plan meant, applying a plan to a period is MOD-0155
/// (MicroTarget), and how many working days a period contains belongs to the working-calendar capability, combined by
/// the consumer. Those routes answer 404 here, and that 404 is part of the contract.</para>
/// <para><c>resolve-active</c> and <c>scope-options</c> are READS: they create nothing and remember nothing.
/// <c>resolve-active</c> answers resolved / none / ambiguous rather than ever guessing a period, and every scope
/// argument it takes is optional — an omitted one means "do not ask at that level", which is what keeps an FU06-shaped
/// call answering exactly what FU06 answered.</para>
/// <para>Under the documented DEV-ONLY fallback the activate/close separation of duty collapses onto manage — a
/// deliberate gap closed by follow-up F-RBAC.</para>
/// </summary>
[Authorize]
public sealed class CyclePeriodsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public CyclePeriodsController(IMediator mediator) => _mediator = mediator;

    /// <summary>The period grid. The scope arguments FILTER; they never resolve.</summary>
    [HttpGet("api/crm/cycle-periods")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] string? cycleStatus,
        [FromQuery] int? year,
        [FromQuery] string? scopeType,
        [FromQuery] string? country,
        [FromQuery] Guid? legalEntityId,
        [FromQuery] string? businessUnitId,
        [FromQuery] string? cycleCode,
        [FromQuery] DateTimeOffset? coversDate,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetCyclePeriodListQuery(
                cycleStatus, year, scopeType, country, legalEntityId, businessUnitId, cycleCode, coversDate, search),
            cancellationToken));

    /// <summary>The lightweight picker a consumer UI binds to.</summary>
    [HttpGet("api/crm/cycle-periods/selector")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Selector(
        [FromQuery] int? year,
        [FromQuery] string? cycleStatus,
        [FromQuery] string? scopeType,
        [FromQuery] string? country,
        [FromQuery] Guid? legalEntityId,
        [FromQuery] string? businessUnitId,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetCyclePeriodSelectorQuery(year, cycleStatus, scopeType, country, legalEntityId, businessUnitId),
            cancellationToken));

    /// <summary>
    /// FU07 — the cascading scope selector's single source: levels, governed country values, referenceable legal
    /// entities and the business units the tenant's territory plans cover for this country and window. Each list
    /// carries its own readiness flag, because "unpublished set", "dependency unreachable" and "no plan matches" are
    /// three different empty lists and an author needs to know which one they are looking at.
    /// </summary>
    [HttpGet("api/crm/cycle-periods/scope-options")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> ScopeOptions(
        [FromQuery] string? country,
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetCyclePeriodScopeOptionsQuery(country, startDate, endDate), cancellationToken));

    /// <summary>"Which period is in force at this instant, at the most specific address I named?" — the HTTP face of
    /// the read-only seam. <c>at</c> is required: defaulting it to "now" would make an audited answer depend on an
    /// unrecorded clock reading.</summary>
    [HttpGet("api/crm/cycle-periods/resolve-active")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> ResolveActive(
        [FromQuery] DateTimeOffset at,
        [FromQuery] string? country,
        [FromQuery] Guid? legalEntityId,
        [FromQuery] string? businessUnitId,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ResolveActiveCyclePeriodQuery(at, country, legalEntityId, businessUnitId), cancellationToken));

    [HttpGet("api/crm/cycle-periods/{cyclePeriodId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid cyclePeriodId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetCyclePeriodByIdQuery(cyclePeriodId), cancellationToken));

    [HttpPost("api/crm/cycle-periods")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCyclePeriodRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateCyclePeriodCommand(
                request.CycleCode, request.CycleName, request.Year, request.SequenceInYear,
                request.StartDate, request.EndDate,
                request.ScopeType, request.CountryScope, request.LegalEntityId, request.BusinessUnitId,
                request.Description,
                request.BusinessUnitCountryContext),
            cancellationToken));

    [HttpPut("api/crm/cycle-periods/{cyclePeriodId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid cyclePeriodId, [FromBody] UpdateCyclePeriodRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateCyclePeriodCommand(
                cyclePeriodId, request.CycleName, request.Year, request.SequenceInYear,
                request.StartDate, request.EndDate,
                request.ScopeType, request.CountryScope, request.LegalEntityId, request.BusinessUnitId,
                request.Description, request.ExpectedVersion,
                request.BusinessUnitCountryContext),
            cancellationToken));

    /// <summary>Puts a period live. The active-overlap ban is enforced HERE, fail-closed and PER SCOPE: on a collision
    /// within the same (ScopeType, ScopeRef) the answer is 409 and the period stays draft. A collision with a period at
    /// another level is not a collision — that is what precedence is for.</summary>
    [HttpPost("api/crm/cycle-periods/{cyclePeriodId:guid}/activate")]
    // Canonical crm.cycle-period.activate (F-RBAC); under the documented DEV-ONLY fallback it collapses onto manage,
    // so the author-is-not-activator separation of duty cannot be enforced in dev.
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Activate(
        Guid cyclePeriodId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ActivateCyclePeriodCommand(cyclePeriodId, expectedVersion), cancellationToken));

    /// <summary>Ends a period. Terminal — there is no reopen endpoint anywhere.</summary>
    [HttpPost("api/crm/cycle-periods/{cyclePeriodId:guid}/close")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Close(
        Guid cyclePeriodId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CloseCyclePeriodCommand(cyclePeriodId, expectedVersion), cancellationToken));
}
