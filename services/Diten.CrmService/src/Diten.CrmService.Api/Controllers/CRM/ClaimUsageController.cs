using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.ContentComposition.Claims.ClaimPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// WP-CL-BE-6 — "where is this claim used?" (content · content set · engagement journey, grouped by country). Kept out
/// of <see cref="ClaimsController"/> on purpose (parallel WP-CL-BE-4). Same <c>/claims</c> prefix, covered by the
/// gateway's <c>/api/crm/content-composition/{everything}</c> route; the literal <c>usage</c> segment never collides
/// with the <c>{claimId:guid}</c> constraint. Read-only, existing <c>crm.claim.read</c> key.
/// </summary>
[Authorize]
public sealed class ClaimUsageController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ClaimUsageController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/content-composition/claims/usage")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Get(
        [FromQuery] string? claimCode,
        [FromQuery] string? countryCode,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetClaimUsageQuery(claimCode ?? string.Empty, countryCode), cancellationToken));
}
