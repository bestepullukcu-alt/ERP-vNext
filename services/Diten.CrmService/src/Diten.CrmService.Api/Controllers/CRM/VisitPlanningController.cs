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
                request.StartLat, request.StartLong, request.ExpectedVersion, request.ManualVisitOrder,
                request.WeekStart),
            cancellationToken));

    /// <summary>WP-VP-3A — reopen an approved week: its visits without a report are cancelled (<c>week_reopened</c>),
    /// reported ones stay; the reason goes into the week's history. Same keys as apply (apply AND planned-visit.manage).</summary>
    [HttpPost("api/crm/visit-plan/sessions/{planningSessionId:guid}/weeks/{weekStart}/reopen")]
    [HasPermission(Perms.Apply)]
    [HasPermission(Perms.PlannedVisitManage)]
    public async Task<IActionResult> ReopenWeek(
        Guid planningSessionId, string weekStart, [FromBody] ReopenPlanningWeekRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ReopenPlanningWeekCommand(planningSessionId, weekStart, request.Reason, request.ExpectedVersion),
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
        // WP-VP-4A — archived plans are listed only when asked for.
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListPlanningSessionsQuery(cyclePeriodId, resourceId, status, includeArchived), cancellationToken));

    /// <summary>WP-VP-2 (B-2) — the accounts the caller's current territory assignments cover (READ query; K-5: no
    /// assignment ⇒ every tenant account with <c>territoryStatus = unassigned</c>). <c>resourceId</c> is honoured only for
    /// a <c>crm.visit-plan.read-all</c> holder.</summary>
    [HttpGet("api/crm/visit-plan/my-accounts")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> MyAccounts(
        [FromQuery] string? search,
        [FromQuery] string? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? resourceId = null,
        // WP-VP-2B (mobile R2) — "true" | "false"; absent ⇒ today's result; anything else ⇒ 400 invalid_has_active_contacts.
        [FromQuery] string? hasActiveContacts = null,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new Application.Features.VisitPlanning.MyAccounts.GetMyAccountsQuery(
                search, type, page, pageSize, resourceId, hasActiveContacts),
            cancellationToken));

    /// <summary>WP-VP-3D (B-6) — an institution's active doctors with each one's period status (required / done /
    /// planned / remaining / last visit / due this week / segment badges / consent / inactive). READ query; the account is
    /// answered even outside the rep's territory, flagged <c>outOfTerritory</c> (K-5).</summary>
    [HttpGet("api/crm/visit-plan/my-accounts/{accountId:guid}/doctors")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> AccountDoctors(
        Guid accountId,
        [FromQuery] Guid? planningSessionId = null,
        [FromQuery] string? quick = null,
        [FromQuery] string? search = null,
        [FromQuery] string? specialty = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? resourceId = null,
        [FromQuery] string? weekStart = null,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new Application.Features.VisitPlanning.TargetStatus.GetAccountDoctorsQuery(
                accountId, planningSessionId, quick, search, specialty, page, pageSize, resourceId, weekStart),
            cancellationToken));

    /// <summary>WP-VP-3D (D5) — the plan's selected institutions, pharmacies and doctors (names, types, places, doctor
    /// statuses) in ONE response. Same ownership as the session read (another rep's plan is 404).</summary>
    [HttpGet("api/crm/visit-plan/sessions/{planningSessionId:guid}/targets")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> SessionTargets(
        Guid planningSessionId, [FromQuery] string? weekStart = null, CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new Application.Features.VisitPlanning.TargetStatus.GetSessionTargetsQuery(planningSessionId, weekStart),
            cancellationToken));

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
                request.RequestedStatus, request.ExpectedVersion, request.TargetWeekStart, request.ToDayPins(),
                request.ToWeekExtras()),
            cancellationToken));
}
