using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.CandidateProfiles;
using Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Commands;
using Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/tep-candidate-profiles")]
[Authorize]
public sealed class CandidateProfilesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public CandidateProfilesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(CandidateProfilePermissions.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCandidateProfileListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(CandidateProfilePermissions.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCandidateProfileByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(CandidateProfilePermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] CandidateProfileRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateCandidateProfileCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(CandidateProfilePermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CandidateProfileRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateCandidateProfileCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission(CandidateProfilePermissions.Manage)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveCandidateProfileCommand(id), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(CandidateProfilePermissions.Evaluate)]
    public async Task<IActionResult> Evaluate(Guid id, [FromBody] EvaluateCandidateProfileRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateCandidateProfileCommand(id, request), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(CandidateProfilePermissions.AuditRead)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCandidateProfileAuditMetadataQuery(id), ct));
}
