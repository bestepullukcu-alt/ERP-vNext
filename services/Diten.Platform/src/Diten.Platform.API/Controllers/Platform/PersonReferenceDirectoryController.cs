using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.PersonReferenceDirectory;
using Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;
using Diten.Platform.Application.Features.PersonReferenceDirectory.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[Route("api/platform/person-reference-directory")]
[Authorize(Policy = "PlatformActor")]
public sealed class PersonReferenceDirectoryController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PersonReferenceDirectoryController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission("platform.person-reference-directory.read")]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPersonReferenceProjectionListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission("platform.person-reference-directory.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPersonReferenceProjectionByIdQuery(id), ct));

    [HttpPost]
    [HasPermission("platform.person-reference-directory.create")]
    public async Task<IActionResult> Create([FromBody] PersonReferenceProjectionCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreatePersonReferenceProjectionCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission("platform.person-reference-directory.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PersonReferenceProjectionUpdateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdatePersonReferenceProjectionCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission("platform.person-reference-directory.archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchivePersonReferenceProjectionCommand(id), ct));

    [HttpPost("{id:guid}/validate")]
    [HasPermission("platform.person-reference-directory.validate")]
    public async Task<IActionResult> Validate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ValidatePersonReferenceProjectionCommand(id), ct));

    [HttpGet("{id:guid}/correlations")]
    [HasPermission("platform.person-reference-directory.correlation.manage")]
    public async Task<IActionResult> GetCorrelations(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPersonReferenceCorrelationsQuery(id), ct));

    [HttpPost("{id:guid}/correlations")]
    [HasPermission("platform.person-reference-directory.correlation.manage")]
    public async Task<IActionResult> CreateCorrelation(Guid id, [FromBody] PersonReferenceExternalCorrelationRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpsertPersonReferenceExternalCorrelationCommand(id, null, request), ct));

    [HttpPut("{id:guid}/correlations/{correlationId:guid}")]
    [HasPermission("platform.person-reference-directory.correlation.manage")]
    public async Task<IActionResult> UpdateCorrelation(Guid id, Guid correlationId, [FromBody] PersonReferenceExternalCorrelationRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpsertPersonReferenceExternalCorrelationCommand(id, correlationId, request), ct));

    [HttpGet("health")]
    [HasPermission("platform.person-reference-directory.health.read")]
    public async Task<IActionResult> GetHealth(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPersonReferenceDirectoryHealthQuery(), ct));
}
