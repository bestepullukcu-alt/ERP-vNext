using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.AssociationOperations;
using Diten.TalentEcosystemService.Application.Features.AssociationOperations.Commands;
using Diten.TalentEcosystemService.Application.Features.AssociationOperations.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/association-operations")]
[Authorize]
public sealed class AssociationOperationsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public AssociationOperationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(AssociationOperationsGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetAssociationOperationsReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(AssociationOperationsGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetAssociationOperationsReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(AssociationOperationsGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] AssociationOperationsReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateAssociationOperationsReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(AssociationOperationsGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateAssociationOperationsReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(AssociationOperationsGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteAssociationOperationsReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(AssociationOperationsGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetAssociationOperationsAuditMetadataQuery(id), ct));
}
