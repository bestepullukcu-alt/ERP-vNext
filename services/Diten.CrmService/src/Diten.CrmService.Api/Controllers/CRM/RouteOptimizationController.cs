using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.RouteOptimization.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.RouteOptimization.RouteOptimizationPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0155 FU03 — Visit Route Optimization: the dry-run route + time-window scheduler preview (pack §11).
/// <para>One endpoint, <c>POST /api/crm/route-optimization/preview</c>. It is a pure CALCULATOR over a supplied visit
/// set: it calls the in-process <see cref="Application.Features.RouteOptimization.IRouteOptimizer"/> seam and returns
/// the schedule, <b>persisting NOTHING</b> — no PlannedVisit write, no Mongo write (applying the schedule onto
/// <c>PlannedVisit.Slot.*</c> is MOD-0155 FU05, not this endpoint). There is no HTML/Razor view — the deliverable is a
/// JSON contract, the <c>calculation-preview</c> precedent.</para>
/// <para>Over-supply / unfittable input is a 200 whose <c>unscheduled[]</c> carries the supply-vs-demand warning
/// (D-UNSCHEDULED — a warning is data, not an HTTP error); only a malformed DTO / out-of-range buffer is a 400.</para>
/// <para><b>Permission.</b> It guards on the new key <c>crm.visit-route.preview</c> (<see cref="Perms.Preview"/>). Its
/// RBAC catalog row + grant are NOT seeded by this pack (F-RBAC), so until an operator grants the key the endpoint
/// answers 403 — the intended fail-closed behaviour. The Gateway route pair is added by the integration-agent (F-GW);
/// before it exists the path returns the known 404 + <c>{}</c> missing-route signature.</para>
/// </summary>
[Authorize]
public sealed class RouteOptimizationController : CustomBaseController
{
    private readonly IMediator _mediator;

    public RouteOptimizationController(IMediator mediator) => _mediator = mediator;

    [HttpPost("api/crm/route-optimization/preview")]
    [HasPermission(Perms.Preview)]
    public async Task<IActionResult> Preview(
        [FromBody] RouteOptimizationPreviewRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new PreviewRouteOptimizationQuery(request.ToInput()), cancellationToken));
}
