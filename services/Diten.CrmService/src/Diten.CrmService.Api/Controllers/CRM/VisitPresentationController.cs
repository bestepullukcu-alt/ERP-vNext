using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitPresentation;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportPerms = Diten.CrmService.Application.Features.VisitReport.VisitReportPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// WP-VW-W3 (W3-BE-b, §2.3) — the visit presentation READ: the slides of one planned visit (approved claims and approved
/// objection answers only), on the existing <c>crm.visit-report.read</c> + ownership (no new key). TenantId is
/// server-resolved.
/// </summary>
[Authorize]
public sealed class VisitPresentationController : CustomBaseController
{
    private readonly IMediator _mediator;

    public VisitPresentationController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/visit-presentation/{plannedVisitId:guid}")]
    [HasPermission(ReportPerms.Read)]
    public async Task<IActionResult> Get(Guid plannedVisitId, [FromQuery] string? lang, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetVisitPresentationQuery(plannedVisitId, lang ?? Request.Headers.AcceptLanguage.ToString()), cancellationToken));

    /// <summary>
    /// The slide image proxy (§2.3 address). Decision 8: the content image lives in the Platform document repository and
    /// is read AS THE CALLER; a rep holds no repository read right (and gets none), so the image cannot be served without a
    /// new Platform permission — every slide says <c>asset.kind = "none"</c> and this address answers 404
    /// <c>presentation_asset_unavailable</c>. A follow-up opens it (a scoped Platform right or a service read).
    /// </summary>
    [HttpGet("api/crm/visit-presentation/{plannedVisitId:guid}/slides/{stepId:guid}/asset")]
    [HasPermission(ReportPerms.Read)]
    public IActionResult Asset(Guid plannedVisitId, Guid stepId)
        => CreateActionResultInstance(Response<bool>.Fail(new[]
        {
            VisitPresentationErrors.AssetUnavailable, "The slide image is not available to the visit presentation (decision 8)."
        }, 404));
}
