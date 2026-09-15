using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.VerifiedParticipants;
using Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Commands;
using Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/tep-verified-participants")]
[Authorize]
public sealed class VerifiedParticipantsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public VerifiedParticipantsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(VerifiedParticipantPermissions.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetVerifiedParticipantAccessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(VerifiedParticipantPermissions.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetVerifiedParticipantAccessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(VerifiedParticipantPermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] VerifiedParticipantAccessRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateVerifiedParticipantAccessCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(VerifiedParticipantPermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] VerifiedParticipantAccessRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateVerifiedParticipantAccessCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission(VerifiedParticipantPermissions.Manage)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveVerifiedParticipantAccessCommand(id), ct));

    [HttpPost("{id:guid}/verify")]
    [HasPermission(VerifiedParticipantPermissions.Verify)]
    public async Task<IActionResult> Verify(Guid id, [FromBody] VerifyParticipantAccessRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new VerifyParticipantAccessCommand(id, request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(VerifiedParticipantPermissions.Evaluate)]
    public async Task<IActionResult> Evaluate(Guid id, [FromBody] EvaluateVerifiedParticipantAccessRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateVerifiedParticipantAccessCommand(id, request), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(VerifiedParticipantPermissions.AuditRead)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetVerifiedParticipantAccessAuditMetadataQuery(id), ct));
}
