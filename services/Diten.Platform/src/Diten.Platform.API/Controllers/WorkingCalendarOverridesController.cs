using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.WorkingCalendar.Commands;
using Diten.Platform.Application.Features.WorkingCalendar.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.Platform.Application.Features.WorkingCalendar.WorkingCalendarPermissions;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// Working Calendar — TENANT OVERRIDE layer (company holidays, closures, compensation working days, optional weekend
/// override). Reachable by any authenticated actor holding the override permissions.
/// <para>
/// <b>Why this is a separate controller.</b> <see cref="WorkingCalendarsController"/> is
/// <c>[Authorize(Policy = "PlatformActor")]</c> at class level, which 403s tenant users, and an action-level
/// <c>[Authorize]</c> cannot relax a class-level policy. Adding tenant endpoints there would have produced a silent,
/// permanent 403 that looks like an RBAC problem. <c>TenantReferenceLookupsController</c> already documents this
/// exact constraint in the codebase.
/// </para>
/// <para>
/// <b>The tenant can read the ACTIVE country layer but cannot write it.</b> List and by-id expose active inherited
/// country rows with <c>IsReadOnly=true</c>; draft/archived country rows stay hidden. Every mutation remains
/// own-override-only and returns 404 for a country id. The tenant also gets the resolved outcome and reason codes,
/// because it cannot author a sensible override while blind to the layer it inherits.
/// </para>
/// <para>
/// The route deliberately nests under <c>/api/platform/working-calendars/overrides</c> so it rides the
/// <c>/api/platform/working-calendars/{everything}</c> Ocelot route rather than needing one of its own. That route
/// did NOT exist when this controller was written — both layers 404ed at the Gateway until it was added; do not
/// assume a wildcard route exists without checking <c>ocelot.json</c>. The Gateway also has to classify this
/// sub-path as tenant-scoped (see <c>TenantResolutionMiddleware.IsTenantScopedOrgPath</c>), otherwise the whole
/// <c>/api/platform/…</c> prefix is treated as admin-only and tenant tokens are 403ed before reaching this class.
/// Its page route, however, is NOT under <c>/Platform/…</c> — that is what makes self-registration derive a Tenant
/// permission scope for these keys.
/// </para>
/// </summary>
[ApiController]
[Authorize]
public sealed class WorkingCalendarOverridesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public WorkingCalendarOverridesController(IMediator mediator) => _mediator = mediator;

    // ── Reads ────────────────────────────────────────────────────────────────

    /// <summary>The tenant slice of the contract: country scope and the country-layer day types are absent, so the
    /// override form structurally cannot offer them even if the page JS is tampered with.</summary>
    [HttpGet("api/platform/working-calendars/overrides/contract")]
    [HasPermission(Perms.OverrideRead)]
    public async Task<IActionResult> GetContract(CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWorkingCalendarContractQuery(TenantSlice: true), ct));

    [HttpGet("api/platform/working-calendars/overrides")]
    [HasPermission(Perms.OverrideRead)]
    public async Task<IActionResult> List(
        [FromQuery] string? countryCode,
        [FromQuery] int? calendarYear,
        [FromQuery] string? scopeType,
        [FromQuery] string? calendarStatus,
        [FromQuery] Guid? organizationUnitId,
        [FromQuery] bool includeArchived,
        CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(
            new ListWorkingCalendarsQuery(
                CountryLayer: false, countryCode, calendarYear, scopeType, calendarStatus, organizationUnitId, includeArchived), ct));

    /// <summary>
    /// The combined answer for a date: country layer + this tenant's override, with the reason codes saying which
    /// layer won. This is the one country-layer fact a tenant may see, and only as an outcome for a date it asked
    /// about — never as the country calendar's contents.
    /// </summary>
    [HttpGet("api/platform/working-calendars/overrides/resolve")]
    [HasPermission(Perms.OverrideRead)]
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

    [HttpGet("api/platform/working-calendars/overrides/{id:guid}")]
    [HasPermission(Perms.OverrideRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWorkingCalendarByIdQuery(id, CountryLayer: false), ct));

    // ── Writes ───────────────────────────────────────────────────────────────

    /// <summary>Creating with <c>scopeType = country</c> is rejected with 403 in the handler — the guard is in the
    /// backend, not merely hidden from the form.</summary>
    [HttpPost("api/platform/working-calendars/overrides")]
    [HasPermission(Perms.OverrideManage)]
    public async Task<IActionResult> Create([FromBody] CreateWorkingCalendarRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(request.ToCommand(isPlatformActor: false), ct));

    [HttpPut("api/platform/working-calendars/overrides/{id:guid}")]
    [HasPermission(Perms.OverrideManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkingCalendarRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(request.ToCommand(id, isPlatformActor: false), ct));

    /// <summary>Activating an override affects only this tenant, so it shares the write permission instead of
    /// carrying its own segregation-of-duties key — unlike the country layer, whose activation reaches everyone.</summary>
    [HttpPost("api/platform/working-calendars/overrides/{id:guid}/activate")]
    [HasPermission(Perms.OverrideManage)]
    public async Task<IActionResult> Activate(Guid id, [FromBody] VersionedActionRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(
            new ActivateWorkingCalendarCommand(id, request.ExpectedVersion, IsPlatformActor: false), ct));

    [HttpPost("api/platform/working-calendars/overrides/{id:guid}/archive")]
    [HasPermission(Perms.OverrideManage)]
    public async Task<IActionResult> Archive(Guid id, [FromBody] VersionedActionRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveWorkingCalendarCommand(id, request.ExpectedVersion, IsPlatformActor: false), ct));

    [HttpPost("api/platform/working-calendars/overrides/{id:guid}/days")]
    [HasPermission(Perms.OverrideManage)]
    public async Task<IActionResult> UpsertDay(Guid id, [FromBody] WorkingCalendarDayRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(
            new UpsertWorkingCalendarDayCommand(id, request.ToInput(), request.ExpectedVersion, IsPlatformActor: false), ct));

    [HttpPost("api/platform/working-calendars/overrides/{id:guid}/days/{dayId:guid}/archive")]
    [HasPermission(Perms.OverrideManage)]
    public async Task<IActionResult> ArchiveDay(Guid id, Guid dayId, [FromBody] VersionedActionRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveWorkingCalendarDayCommand(id, dayId, request.ExpectedVersion, IsPlatformActor: false), ct));
}
