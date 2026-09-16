using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry;
using Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry.Commands;
using Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/restricted-integrity-registry")]
[Authorize]
public sealed class RestrictedIntegrityRegistryController : CustomBaseController
{
    private readonly IMediator _mediator;

    public RestrictedIntegrityRegistryController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(RestrictedIntegrityRegistryGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetRestrictedIntegrityRegistryReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(RestrictedIntegrityRegistryGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetRestrictedIntegrityRegistryReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(RestrictedIntegrityRegistryGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] RestrictedIntegrityRegistryReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateRestrictedIntegrityRegistryReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(RestrictedIntegrityRegistryGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateRestrictedIntegrityRegistryReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(RestrictedIntegrityRegistryGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteRestrictedIntegrityRegistryReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(RestrictedIntegrityRegistryGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetRestrictedIntegrityRegistryAuditMetadataQuery(id), ct));
}
