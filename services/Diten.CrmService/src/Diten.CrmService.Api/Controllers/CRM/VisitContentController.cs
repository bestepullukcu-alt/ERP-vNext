using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.VisitContentSequence.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.VisitContentSequence.VisitContentSequencePermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0155 FU04 — Visit Content Sequence: the read-only "which content is next + how long does the visit take?"
/// preview (pack §14, D-SURFACE = E).
/// <para>One endpoint, <c>POST /api/crm/visit-content/preview</c>. It is a thin wrapper over the in-process
/// <see cref="Application.Features.VisitContentSequence.VisitContentSequenceResolver"/> seam and returns the resolved
/// result, <b>persisting NOTHING</b> — writing the chosen position onto <c>PlannedVisit.Content</c> is MOD-0155 FU01,
/// not this endpoint. There is no HTML/Razor view — the deliverable is a JSON contract (the <c>route-optimization</c> /
/// <c>calculation-preview</c> precedent).</para>
/// <para>Every content / journey / capacity gap is a 200 whose <c>status</c> + <c>reasonCodes</c> carry the coded
/// outcome (a coded gap is data, not an HTTP error); only a missing subject id is a 400.</para>
/// <para><b>Permission.</b> It guards on the new key <c>crm.visit-content.preview</c> (<see cref="Perms.Preview"/>). Its
/// RBAC catalog row + grant are NOT seeded by this pack (F-RBAC-VISIT-CONTENT), so the endpoint answers 403 until an
/// operator grants the key — the intended fail-closed behaviour. The Gateway route pair for
/// <c>/api/crm/visit-content/{everything}</c> is owned by the integration-agent (F-OCELOT-VISIT-CONTENT).</para>
/// </summary>
[Authorize]
public sealed class VisitContentController : CustomBaseController
{
    private readonly IMediator _mediator;

    public VisitContentController(IMediator mediator) => _mediator = mediator;

    [HttpPost("api/crm/visit-content/preview")]
    [HasPermission(Perms.Preview)]
    public async Task<IActionResult> Preview(
        [FromBody] VisitContentPreviewRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new PreviewVisitContentQuery((request ?? new VisitContentPreviewRequest()).ToRequest()), cancellationToken));
}
