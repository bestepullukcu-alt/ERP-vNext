using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.VisitReport.Commands;
using Diten.CrmService.Application.Features.VisitReport.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.VisitReport.VisitReportPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0155 FU02 — VisitReport: the EXECUTION counterpart of FU05's setup console. The rep opens the Day/Week execution
/// calendar, marks a visit done/missed/rescheduled, records the immutable Visit Report, and files append-only amendments.
/// <para>There is <b>no DELETE and no bulk-delete</b> anywhere in this controller: a report is a compliance record.
/// <c>read</c> is split from <c>record</c>/<c>amend</c> (D-RBAC); <c>record</c> ALSO requires FU01
/// <c>crm.planned-visit.manage</c>. Under the documented DEV-ONLY fallback these collapse onto territory keys, so the
/// read/record/amend split cannot be enforced in dev (F-RBAC). TenantId is server-resolved from the claim.</para>
/// </summary>
[Authorize]
public sealed class VisitReportController : CustomBaseController
{
    private readonly IMediator _mediator;

    public VisitReportController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/visit-report/contract")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Contract(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetVisitReportContractQuery(), cancellationToken));

    /// <summary>The Day/Week execution calendar: FU01 plan atoms in the window joined with their FU02 report state.</summary>
    [HttpGet("api/crm/visit-report/calendar")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Calendar(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? resourceId,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetVisitCalendarQuery(from, to, resourceId), cancellationToken));

    [HttpGet("api/crm/visit-report")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? plannedVisitId,
        [FromQuery] string? reportStatus,
        [FromQuery] string? executionOutcome,
        [FromQuery] string? resourceId,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListVisitReportsQuery(plannedVisitId, reportStatus, executionOutcome, resourceId), cancellationToken));

    [HttpGet("api/crm/visit-report/{visitReportId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid visitReportId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetVisitReportByIdQuery(visitReportId), cancellationToken));

    /// <summary>Marks a visit done/missed/rescheduled. Canonical crm.visit-report.record + FU01 crm.planned-visit.manage;
    /// under the DEV-ONLY fallback both collapse onto the territory manage key (F-RBAC).</summary>
    [HttpPost("api/crm/visit-report/outcome")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> RecordOutcome(
        [FromBody] RecordVisitOutcomeRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new RecordVisitOutcomeCommand(
                request.PlannedVisitId, request.ExecutionOutcome, request.ExecutedAt, request.ReasonCode,
                request.RescheduleToDate, request.RescheduleNotes, request.ReportedByResourceId, request.ExpectedVersion),
            cancellationToken));

    /// <summary>Records + submits a completed visit's report (immutable after the correction window).</summary>
    [HttpPost("api/crm/visit-report")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitVisitReportRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new SubmitVisitReportCommand(
                request.PlannedVisitId, request.ContentActuals, request.Samples, request.Feedback,
                request.ExecutedAt, request.ReportedByResourceId, request.ExpectedVersion),
            cancellationToken));

    /// <summary>Files an append-only amendment to a finalised report (D-EDIT-WINDOW).</summary>
    [HttpPost("api/crm/visit-report/{visitReportId:guid}/amend")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Amend(
        Guid visitReportId, [FromBody] AmendVisitReportRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new AmendVisitReportCommand(
                visitReportId, request.Reason, request.ReportedByResourceId,
                request.ContentActuals, request.Samples, request.Feedback, request.ExpectedVersion),
            cancellationToken));
}
