using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.API.Models;
using Diten.Platform.Application.Features.WorkingCalendar;
using Diten.Platform.Application.Features.WorkingCalendar.Commands;
using Diten.Platform.Application.Features.WorkingCalendar.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.Platform.Application.Features.WorkingCalendar.WorkingCalendarPermissions;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// Working Calendar — COUNTRY layer (weekend definition + official/religious holidays). Platform actors only.
/// <para>
/// The tenant override layer lives in a SEPARATE controller, not in extra actions here. A class-level
/// <c>PlatformActor</c> policy 403s tenant users and an action-level <c>[Authorize]</c> cannot relax it — the same
/// constraint that forced <c>TenantReferenceLookupsController</c> to exist. Two audiences therefore need two
/// controllers.
/// </para>
/// <para>
/// Every <c>{id}</c> carries a <c>:guid</c> route constraint on purpose: without it
/// <c>working-calendars/overrides</c> would bind to <c>working-calendars/{id}</c> here and tenant traffic would land
/// on the platform controller, surfacing as a confusing 403 that is really a routing bug.
/// </para>
/// <para><b>There is no delete endpoint.</b> A calendar is archived so history stays readable.</para>
/// </summary>
[ApiController]
[Authorize(Policy = "PlatformActor")]
public sealed class WorkingCalendarsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public WorkingCalendarsController(IMediator mediator) => _mediator = mediator;

    // ── Reads ────────────────────────────────────────────────────────────────

    /// <summary>Supported vocabulary, limits and permissions. Every dropdown on the admin page is fed from here —
    /// no view or JS file carries a hardcoded vocabulary list.</summary>
    [HttpGet("api/platform/working-calendars/contract")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> GetContract(CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWorkingCalendarContractQuery(TenantSlice: false), ct));

    [HttpGet("api/platform/working-calendars")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> List(
        [FromQuery] string? countryCode,
        [FromQuery] int? calendarYear,
        [FromQuery] string? scopeType,
        [FromQuery] string? calendarStatus,
        [FromQuery] bool includeArchived,
        CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(
            new ListWorkingCalendarsQuery(
                CountryLayer: true, countryCode, calendarYear, scopeType, calendarStatus, null, includeArchived), ct));

    /// <summary>
    /// Read-only working-day resolution. Never writes. An unresolved answer ("no calendar entered for this
    /// country/year") comes back as 200 with <c>resolution</c> set and a null value — it is a legitimate answer the
    /// caller must handle, not a server error.
    /// </summary>
    [HttpGet("api/platform/working-calendars/resolve")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Resolve(
        [FromQuery] string op,
        [FromQuery] DateOnly date,
        [FromQuery] string countryCode,
        [FromQuery] Guid? organizationUnitId,
        [FromQuery] Guid? legalEntityId,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int? days,
        CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(
            new ResolveWorkingDayQuery(op, date, countryCode, organizationUnitId, legalEntityId, toDate, days), ct));

    [HttpGet("api/platform/working-calendars/{id:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWorkingCalendarByIdQuery(id, CountryLayer: true), ct));

    // ── Writes ───────────────────────────────────────────────────────────────

    [HttpPost("api/platform/working-calendars")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Create([FromBody] CreateWorkingCalendarRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(request.ToCommand(isPlatformActor: true), ct));

    [HttpPut("api/platform/working-calendars/{id:guid}")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkingCalendarRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(request.ToCommand(id, isPlatformActor: true), ct));

    /// <summary>
    /// Activating a COUNTRY calendar changes the working-day answer for every tenant in that country — the widest
    /// blast radius in this module — so it sits behind its own permission rather than sharing the write key.
    /// </summary>
    [HttpPost("api/platform/working-calendars/{id:guid}/activate")]
    [HasPermission(Perms.Activate)]
    public async Task<IActionResult> Activate(Guid id, [FromBody] VersionedActionRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(
            new ActivateWorkingCalendarCommand(id, request.ExpectedVersion, IsPlatformActor: true), ct));

    [HttpPost("api/platform/working-calendars/{id:guid}/archive")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Archive(Guid id, [FromBody] VersionedActionRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveWorkingCalendarCommand(id, request.ExpectedVersion, IsPlatformActor: true), ct));

    [HttpPost("api/platform/working-calendars/{id:guid}/days")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> UpsertDay(Guid id, [FromBody] WorkingCalendarDayRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(
            new UpsertWorkingCalendarDayCommand(id, request.ToInput(), request.ExpectedVersion, IsPlatformActor: true), ct));

    [HttpPost("api/platform/working-calendars/{id:guid}/days/{dayId:guid}/archive")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> ArchiveDay(Guid id, Guid dayId, [FromBody] VersionedActionRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveWorkingCalendarDayCommand(id, dayId, request.ExpectedVersion, IsPlatformActor: true), ct));
}
