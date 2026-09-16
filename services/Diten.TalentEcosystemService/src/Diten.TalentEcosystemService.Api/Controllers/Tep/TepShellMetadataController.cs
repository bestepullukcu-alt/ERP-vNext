using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.TepShell;
using Diten.TalentEcosystemService.Application.Features.TepShell.Commands;
using Diten.TalentEcosystemService.Application.Features.TepShell.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/tep-shell-metadata")]
[Authorize]
public sealed class TepShellMetadataController : CustomBaseController
{
    private readonly IMediator _mediator;

    public TepShellMetadataController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(TepShellPermissions.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTepShellMetadataListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(TepShellPermissions.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTepShellMetadataByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(TepShellPermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] TepShellMetadataRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateTepShellMetadataCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(TepShellPermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] TepShellMetadataRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateTepShellMetadataCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission(TepShellPermissions.Manage)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveTepShellMetadataCommand(id), ct));

    [HttpGet("health")]
    [HasPermission(TepShellPermissions.Read)]
    public async Task<IActionResult> GetHealth(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTepShellHealthQuery(), ct));
}
