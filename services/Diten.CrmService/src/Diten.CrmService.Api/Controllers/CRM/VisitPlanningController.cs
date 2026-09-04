using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.VisitPlanning.Commands;
using Diten.CrmService.Application.Features.VisitPlanning.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.VisitPlanning.VisitPlanningPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0155 FU05 — the MicroTarget Visit Planning Engine setup endpoints (pack §15). One wildcard route pair
/// (<c>/api/crm/visit-plan/{everything}</c>) covers preview + apply + re-plan + session CRUD; the Gateway route is
/// declared for the integration-agent (F-GW), so until it exists these paths return the 404 + <c>{}</c> missing-route
/// signature.
/// <para><b>Permissions (D-RBAC = B, split, LOCKED).</b> Reads take <see cref="Perms.Read"/>; preview + session
/// create/edit take <see cref="Perms.Generate"/>; apply + re-plan stack <see cref="Perms.Apply"/> AND the FU01
/// <see cref="Perms.PlannedVisitManage"/> key (both must pass — they write through FU01's aggregate). The real keys sit
/// on the endpoints with NO territory fallback, so each answers 403 until an operator grants the key (F-RBAC) — the
/// intended fail-closed behaviour, mirroring FU03's <c>crm.visit-route.preview</c>.</para>
/// <para><b>preview persists NOTHING</b> (dry-run); <b>apply</b> writes FU01 atoms + commits the session atomically;
/// <b>re-plan</b> updates a subset in place. Any bodiless 204 uses the shared proxy guard downstream.</para>
/// </summary>
[Authorize]
public sealed class VisitPlanningController : CustomBaseController
{
    private readonly IMediator _mediator;

    public VisitPlanningController(IMediator mediator) => _mediator = mediator;

    // ── generation ──────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Dry-run preview (①–⑦). Persists nothing.</summary>
    [HttpPost("api/crm/visit-plan/preview")]
    [HasPermission(Perms.Generate)]
    public async Task<IActionResult> Preview(
        [FromBody] GeneratePlanPreviewRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GeneratePlanPreviewQuery(
                request.PlanningSessionId, request.VisitPurpose, request.VisitType,
                request.StartLat, request.StartLong, request.ManualVisitOrder),
            cancellationToken));

    /// <summary>Apply: write FU01 atoms + commit the session, atomically. Requires apply AND FU01 planned-visit.manage.</summary>
    [HttpPost("api/crm/visit-plan/apply")]
    [HasPermission(Perms.Apply)]
    [HasPermission(Perms.PlannedVisitManage)]
    public async Task<IActionResult> Apply(
        [FromBody] ApplyPlanRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ApplyPlanningSessionCommand(
                request.PlanningSessionId, request.VisitPurpose, request.VisitType,
                request.StartLat, request.StartLong, request.ExpectedVersion, request.ManualVisitOrder),
            cancellationToken));

    /// <summary>Re-plan a subset in place. Requires apply AND FU01 planned-visit.manage.</summary>
    [HttpPost("api/crm/visit-plan/re-plan")]
    [HasPermission(Perms.Apply)]
    [HasPermission(Perms.PlannedVisitManage)]
    public async Task<IActionResult> Replan(
        [FromBody] ReplanPlanRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ReplanPlanningSessionCommand(
                request.PlanningSessionId, request.AffectedContactIds, request.VisitPurpose,
                request.VisitType, request.StartLat, request.StartLong, request.ManualVisitOrder),
            cancellationToken));

    // ── session CRUD (the staging record; D-PERSISTENCE = C) ───────────────────────────────────────────────────────

    [HttpGet("api/crm/visit-plan/sessions")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> ListSessions(
        [FromQuery] Guid? cyclePeriodId,
        [FromQuery] string? resourceId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ListPlanningSessionsQuery(cyclePeriodId, resourceId, status), cancellationToken));

    [HttpGet("api/crm/visit-plan/sessions/{planningSessionId:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> GetSession(Guid planningSessionId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetPlanningSessionByIdQuery(planningSessionId), cancellationToken));

    [HttpPost("api/crm/visit-plan/sessions")]
    [HasPermission(Perms.Generate)]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreatePlanningSessionRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreatePlanningSessionCommand(
                request.CyclePeriodId, request.ResourceId, request.ResourceType, request.ResourceDisplayName,
                request.SelectedAccountIds, request.SelectedPharmacyIds, request.ToContacts(),
                request.SegmentId, request.CampaignId, request.StrategyTemplateId, request.TargetWeekStart),
            cancellationToken));

    [HttpPut("api/crm/visit-plan/sessions/{planningSessionId:guid}")]
    [HasPermission(Perms.Generate)]
    public async Task<IActionResult> UpdateSession(
        Guid planningSessionId, [FromBody] UpdatePlanningSessionRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdatePlanningSessionSelectionCommand(
                planningSessionId, request.SelectedAccountIds, request.SelectedPharmacyIds, request.ToContacts(),
                request.SegmentId, request.CampaignId, request.StrategyTemplateId,
                request.RequestedStatus, request.ExpectedVersion, request.TargetWeekStart),
            cancellationToken));
}
