using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.Knowledge.AudienceProfile.Commands;
using Diten.CrmService.Application.Features.Knowledge.AudienceProfile.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.KnowledgePermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0162 FU02 — AudienceProfile authoring (generic profile; a DoctorProfile is not a separate entity). Canonical
/// under <c>/api/crm/knowledge/audience-profiles</c>. Uses the subject-taxonomy permission pair on the documented
/// territory fallback until MOD-0162-FU02-RBAC lands. <b>No delete endpoint</b>: closing a profile is Archive.
/// </summary>
[Authorize]
public sealed class KnowledgeAudienceProfilesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public KnowledgeAudienceProfilesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("api/crm/knowledge/audience-profiles")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? profileType,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListAudienceProfilesQuery(status, profileType, search, includeArchived), cancellationToken));

    [HttpGet("api/crm/knowledge/audience-profiles/{audienceProfileId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid audienceProfileId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetAudienceProfileQuery(audienceProfileId), cancellationToken));

    [HttpPost("api/crm/knowledge/audience-profiles")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAudienceProfileRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateAudienceProfileCommand(
                request.ProfileCode, request.ProfileName, request.EffectiveFrom, request.Description,
                request.ProfileType, request.Status, request.SortOrder, request.EffectiveTo, request.Alias,
                request.ExternalReferences),
            cancellationToken));

    [HttpPut("api/crm/knowledge/audience-profiles/{audienceProfileId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid audienceProfileId,
        [FromBody] UpdateAudienceProfileRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateAudienceProfileCommand(
                audienceProfileId, request.ProfileName, request.EffectiveFrom, request.Description,
                request.ProfileType, request.Status, request.SortOrder, request.EffectiveTo, request.Alias,
                request.ExternalReferences),
            cancellationToken));

    [HttpPost("api/crm/knowledge/audience-profiles/{audienceProfileId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(Guid audienceProfileId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveAudienceProfileCommand(audienceProfileId), cancellationToken));

    [HttpPost("api/crm/knowledge/audience-profiles/{audienceProfileId:guid}/unarchive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Unarchive(Guid audienceProfileId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UnarchiveAudienceProfileCommand(audienceProfileId), cancellationToken));
}
