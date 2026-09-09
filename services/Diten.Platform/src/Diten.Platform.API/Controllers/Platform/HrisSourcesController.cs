using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.HrisSources;
using Diten.Platform.Application.Features.HrisSources.Commands;
using Diten.Platform.Application.Features.HrisSources.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[Route("api/platform/hris-sources")]
[Authorize(Policy = "PlatformActor")]
public sealed class HrisSourcesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public HrisSourcesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission("platform.hris-sources.read")]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHrisSourceProfileListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission("platform.hris-sources.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHrisSourceProfileByIdQuery(id), ct));

    [HttpPost]
    [HasPermission("platform.hris-sources.create")]
    public async Task<IActionResult> Create([FromBody] HrisSourceProfileCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateHrisSourceProfileCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission("platform.hris-sources.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] HrisSourceProfileUpdateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateHrisSourceProfileCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission("platform.hris-sources.archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveHrisSourceProfileCommand(id), ct));

    [HttpPost("{id:guid}/validate-connection")]
    [HasPermission("platform.hris-sources.validate-connection")]
    public async Task<IActionResult> ValidateConnection(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ValidateHrisSourceConnectionCommand(id), ct));

    [HttpPut("{id:guid}/mapping-profile")]
    [HasPermission("platform.hris-sources.update-mapping")]
    public async Task<IActionResult> UpdateMappingProfile(Guid id, [FromBody] HrisMappingProfileRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateHrisMappingProfileCommand(id, request), ct));

    [HttpGet("{id:guid}/sync-checkpoint")]
    [HasPermission("platform.hris-sources.read-health")]
    public async Task<IActionResult> GetSyncCheckpoint(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHrisSyncCheckpointQuery(id), ct));

    [HttpGet("{id:guid}/health")]
    [HasPermission("platform.hris-sources.read-health")]
    public async Task<IActionResult> GetHealth(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHrisSourceHealthQuery(id), ct));
}
