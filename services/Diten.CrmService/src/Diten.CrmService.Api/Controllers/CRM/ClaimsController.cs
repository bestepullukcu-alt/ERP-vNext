using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.ContentComposition.Claims.ClaimPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// SCMM-12-API (CAND-CAP-0011) — Claim authoring HTTP surface. Canonical under
/// <c>/api/crm/content-composition/claims</c>. A thin adapter over the ready Claim CQRS (SCMM-12): every action just
/// maps the request onto the command/query and dispatches through MediatR — no business logic lives here. Approval is a
/// dedicated action (draft → approved); there is <b>no delete endpoint</b> — closing a claim is Archive.
/// </summary>
[Authorize]
public sealed class ClaimsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ClaimsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/content-composition/claims")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? effectiveAt,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListClaimsQuery(status, effectiveAt, search, includeArchived), cancellationToken));

    [HttpGet("api/crm/content-composition/claims/{claimId:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Get(Guid claimId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetClaimQuery(claimId), cancellationToken));

    [HttpPost("api/crm/content-composition/claims")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Create(
        [FromBody] CreateClaimRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateClaimCommand(
                request.ClaimCode, request.ClaimName, request.ClaimText, request.EffectiveFrom, request.Description,
                request.Qualifiers, ToApplicabilityInput(request.Applicability), request.EvidenceRefs,
                request.ComponentRefs, request.ClaimVersion, request.Status, request.EffectiveTo),
            cancellationToken));

    [HttpPut("api/crm/content-composition/claims/{claimId:guid}")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Update(
        Guid claimId, [FromBody] UpdateClaimRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateClaimCommand(
                claimId, request.ClaimName, request.ClaimText, request.EffectiveFrom, request.Description,
                request.Qualifiers, ToApplicabilityInput(request.Applicability), request.EvidenceRefs,
                request.ComponentRefs, request.ClaimVersion, request.Status, request.EffectiveTo),
            cancellationToken));

    [HttpPost("api/crm/content-composition/claims/{claimId:guid}/approve")]
    [HasPermission(Perms.Approve)]
    public async Task<IActionResult> Approve(Guid claimId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ApproveClaimCommand(claimId), cancellationToken));

    [HttpPost("api/crm/content-composition/claims/{claimId:guid}/archive")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Archive(Guid claimId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ArchiveClaimCommand(claimId), cancellationToken));

    // Maps the API applicability request shape onto the application command input. Null stays null.
    private static ClaimApplicabilityInput? ToApplicabilityInput(ClaimApplicabilityRequest? applicability)
        => applicability is null
            ? null
            : new ClaimApplicabilityInput(
                applicability.ProductRefs, applicability.MarketRefs, applicability.AudienceRefs,
                applicability.EligibilityPolicyId);
}
