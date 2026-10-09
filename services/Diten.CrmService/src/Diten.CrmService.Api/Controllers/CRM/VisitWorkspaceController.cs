using Diten.CrmService.Application.Features.VisitWorkspace.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanPerms = Diten.CrmService.Application.Features.VisitPlanning.VisitPlanningPermissions;
using ReportPerms = Diten.CrmService.Application.Features.VisitReport.VisitReportPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// WP-VW-W2 — the visit workspace READS (calendar, reasons, reschedule options, contract). Every action needs BOTH
/// <c>crm.visit-report.read</c> and <c>crm.visit-plan.read</c>; the writes stay on the existing planned-visit /
/// visit-report / visit-plan endpoints with their own keys. TenantId is server-resolved.
/// </summary>
[Authorize]
public sealed class VisitWorkspaceController : CustomBaseController
{
    private readonly IMediator _mediator;

    public VisitWorkspaceController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/visit-workspace/contract")]
    [HasPermission(ReportPerms.Read)]
    [HasPermission(PlanPerms.Read)]
    public async Task<IActionResult> Contract(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetVisitWorkspaceContractQuery(), cancellationToken));

    /// <summary>The unified calendar of [from, to] (≤ 42 days): written visits, draft-week previews, weeks, days.</summary>
    [HttpGet("api/crm/visit-workspace/calendar")]
    [HasPermission(ReportPerms.Read)]
    [HasPermission(PlanPerms.Read)]
    public async Task<IActionResult> Calendar(
        [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? resourceId,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetWorkspaceCalendarQuery(from, to, resourceId), cancellationToken));

    /// <summary>The active reasons for cancel / missed / reschedule, labelled by <c>?lang=</c> or Accept-Language.</summary>
    [HttpGet("api/crm/visit-workspace/reasons")]
    [HasPermission(ReportPerms.Read)]
    [HasPermission(PlanPerms.Read)]
    public async Task<IActionResult> Reasons(
        [FromQuery] string? appliesTo, [FromQuery] string? lang, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetVisitReasonsQuery(appliesTo, lang ?? Request.Headers.AcceptLanguage.ToString()), cancellationToken));

    /// <summary>The next 8 working days the visit can be moved to, with the rep's load.</summary>
    [HttpGet("api/crm/visit-workspace/reschedule-options")]
    [HasPermission(ReportPerms.Read)]
    [HasPermission(PlanPerms.Read)]
    public async Task<IActionResult> RescheduleOptions(
        [FromQuery] Guid plannedVisitId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetRescheduleOptionsQuery(plannedVisitId), cancellationToken));
}
