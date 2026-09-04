using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.CycleCapacity;
using Diten.CrmService.Application.Features.CycleCapacity.Commands;
using Diten.CrmService.Application.Features.CycleCapacity.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.CycleCapacity.CycleCapacityPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0155 FU06 — Cycle Capacity: how many visits fit in one cycle period, as an ESTIMATE.
/// <para>There is <b>no DELETE, no PATCH and no bulk-delete</b> anywhere in this controller (retiring a capacity is
/// <c>archive</c>), and deliberately <b>no /generate, /apply, /distribute, /approve and no /working-days</b> path:
/// producing MicroTarget rows is MOD-0155 FU05, allocating visits to representatives is not a capacity question,
/// approving an estimate is follow-up F-APPROVAL, and authoring working days belongs to the platform working calendar.
/// Those routes answer 404 here, and that 404 is part of the contract.</para>
/// <para><c>/calculation</c> is a READ that reaches the working calendar. It writes nothing — not the figure, not a
/// cache — and it is fail-closed: one month that cannot be resolved makes the whole answer unresolved
/// (<b>503</b>, with the resolution and reason codes in the body) rather than a partial table. A <c>null</c> total is
/// "we do not know"; <c>0</c> is "no time is left". They are never conflated.</para>
/// <para>Under the documented DEV-ONLY fallback both keys collapse onto the territory permissions — a deliberate gap
/// closed by follow-up F-RBAC.</para>
/// </summary>
[Authorize]
public sealed class CycleCapacitiesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public CycleCapacitiesController(IMediator mediator) => _mediator = mediator;

    /// <summary>The capacity grid. It returns authored INPUTS plus the pinned period's identifying fields, and no
    /// estimate — computing one per row would turn a grid draw into dozens of cross-service calls.</summary>
    [HttpGet("api/crm/cycle-capacities")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? cyclePeriodId,
        [FromQuery] string? calendarCountryCode,
        [FromQuery] bool includeArchived,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetCycleCapacityListQuery(cyclePeriodId, calendarCountryCode, includeArchived, search),
            cancellationToken));

    /// <summary>"Does this period already have a capacity?" — the lookup behind the CyclePeriod row action. A 404 here
    /// means "not yet" and the UI turns it into the create form; it is an expected answer, not an error.</summary>
    [HttpGet("api/crm/cycle-capacities/by-cycle-period/{cyclePeriodId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> GetByCyclePeriod(Guid cyclePeriodId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetCycleCapacityByCyclePeriodQuery(cyclePeriodId), cancellationToken));

    [HttpGet("api/crm/cycle-capacities/{cycleCapacityId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid cycleCapacityId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetCycleCapacityByIdQuery(cycleCapacityId), cancellationToken));

    /// <summary>
    /// The estimate, computed fresh from the published working calendar. Never stored, never cached, and never
    /// partial.
    /// <para>There is no <c>.calculate</c> permission: the figure is a VIEW over inputs the reader can already see.
    /// The caller does, however, need the platform's <c>platform.working-calendar.override.read</c> — without it the
    /// answer comes back as <c>calendar_forbidden</c>, which is deliberately distinguishable from "no calendar exists"
    /// (F-RBAC-WC).</para>
    /// </summary>
    [HttpGet("api/crm/cycle-capacities/{cycleCapacityId:guid}/calculation")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Calculation(Guid cycleCapacityId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetCycleCapacityCalculationQuery(cycleCapacityId), cancellationToken));

    /// <summary>
    /// The LIVE estimate, from form inputs rather than from a stored row — what the create/edit page calls while an
    /// author is still typing.
    /// <para>It is a POST because it carries a body, and a QUERY in every other respect: <b>it creates nothing and
    /// stores nothing</b>, and the answer has no id to save against. The capacity it estimates exists only for the
    /// duration of the request.</para>
    /// <para>It guards on <c>read</c>, not <c>manage</c>: seeing what a set of numbers would produce is a reading of
    /// data the caller may already read, and requiring the authoring permission would hide the preview from someone
    /// allowed to look at the record it previews.</para>
    /// <para>Fail-closed like its saved sibling: if the working calendar cannot answer for any month, the whole
    /// estimate comes back 503 with the resolution and reason codes intact — never a partial table.</para>
    /// </summary>
    [HttpPost("api/crm/cycle-capacities/calculation-preview")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> CalculationPreview(
        [FromBody] PreviewCycleCapacityRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new PreviewCycleCapacityCalculationQuery(
                request.CyclePeriodId,
                request.CalendarCountryCode,
                request.DailyWorkMinutes,
                request.PromoProductTime,
                request.NonPromoProductTime,
                request.TravelingTime,
                request.ReportDuration,
                request.QuizDuration,
                ToMonths(request.Months)),
            cancellationToken));

    [HttpPost("api/crm/cycle-capacities")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCycleCapacityRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateCycleCapacityCommand(
                request.CyclePeriodId,
                request.CalendarCountryCode,
                request.DailyWorkMinutes,
                request.PromoProductTime,
                request.NonPromoProductTime,
                request.TravelingTime,
                request.ReportDuration,
                request.QuizDuration,
                request.Description,
                ToMonths(request.Months),
                request.BetweenVisitTimeMinutes),
            cancellationToken));

    /// <summary>An edit. The route carries the capacity's own id and the body carries no cycle period at all: the pin
    /// cannot be expressed, so it cannot be moved.</summary>
    [HttpPut("api/crm/cycle-capacities/{cycleCapacityId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid cycleCapacityId, [FromBody] UpdateCycleCapacityRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateCycleCapacityCommand(
                cycleCapacityId,
                request.CalendarCountryCode,
                request.DailyWorkMinutes,
                request.PromoProductTime,
                request.NonPromoProductTime,
                request.TravelingTime,
                request.ReportDuration,
                request.QuizDuration,
                request.Description,
                ToMonths(request.Months),
                request.ExpectedVersion,
                request.BetweenVisitTimeMinutes),
            cancellationToken));

    /// <summary>Retires a capacity — a SOFT archive that also frees its period for a fresh one. There is no delete
    /// endpoint anywhere in this feature.</summary>
    [HttpPost("api/crm/cycle-capacities/{cycleCapacityId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(
        Guid cycleCapacityId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveCycleCapacityCommand(cycleCapacityId, expectedVersion), cancellationToken));

    private static IReadOnlyList<CycleCapacityMonthInput> ToMonths(IEnumerable<CycleCapacityMonthRequest>? months)
        => months?.Select(m => new CycleCapacityMonthInput(
                   m.Year, m.MonthNumber, m.MeetingDays, m.TrainingDays, m.VacationDays,
                   m.MicroTargetingDayCount, m.MicroTargetingDuration))
               .ToList()
           ?? new List<CycleCapacityMonthInput>();
}
