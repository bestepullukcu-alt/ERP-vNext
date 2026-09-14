using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry;
using Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry.Commands;
using Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/verified-certification-registry")]
[Authorize]
public sealed class VerifiedCertificationRegistryController : CustomBaseController
{
    private readonly IMediator _mediator;

    public VerifiedCertificationRegistryController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(VerifiedCertificationRegistryGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetVerifiedCertificationRegistryReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(VerifiedCertificationRegistryGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetVerifiedCertificationRegistryReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(VerifiedCertificationRegistryGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] VerifiedCertificationRegistryReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateVerifiedCertificationRegistryReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(VerifiedCertificationRegistryGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateVerifiedCertificationRegistryReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(VerifiedCertificationRegistryGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteVerifiedCertificationRegistryReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(VerifiedCertificationRegistryGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetVerifiedCertificationRegistryAuditMetadataQuery(id), ct));
}
