using Diten.MdmService.Application.Features.LegalEntity;
using Diten.MdmService.Application.Features.LegalEntity.Commands;
using Diten.MdmService.Application.Features.LegalEntity.Queries;
using Diten.MdmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/legal-entities")]
public sealed class LegalEntitiesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public LegalEntitiesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // MOD-0288 lookup feed (no filters) + MOD-0220 FULL filtered list. Filters bind from the query string.
    [HttpGet]
    [HasPermission("mdm.legal-entities.read")]
    public async Task<IActionResult> GetAll([FromQuery] GetLegalEntitiesQuery query, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(query, cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpGet("lookup")]
    [HasPermission("mdm.legal-entities.read")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetLegalEntitiesLookupQuery(), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{legalEntityId:guid}")]
    [HasPermission("mdm.legal-entities.read")]
    public async Task<IActionResult> GetById(Guid legalEntityId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetLegalEntityByIdQuery(legalEntityId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{legalEntityId:guid}/lookup-validation")]
    [HasPermission("mdm.legal-entities.read")]
    public async Task<IActionResult> ValidateReference(Guid legalEntityId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ValidateLegalEntityReferenceQuery(legalEntityId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    [HasPermission("mdm.legal-entities.create")]
    public async Task<IActionResult> Create([FromBody] LegalEntityWriteRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new CreateLegalEntityCommand(request), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{legalEntityId:guid}")]
    [HasPermission("mdm.legal-entities.update")]
    public async Task<IActionResult> Update(Guid legalEntityId, [FromBody] LegalEntityWriteRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new UpdateLegalEntityCommand(legalEntityId, request), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPatch("{legalEntityId:guid}/activate")]
    [HasPermission("mdm.legal-entities.update")]
    public async Task<IActionResult> Activate(Guid legalEntityId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ActivateLegalEntityCommand(legalEntityId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPatch("{legalEntityId:guid}/suspend")]
    [HasPermission("mdm.legal-entities.update")]
    public async Task<IActionResult> Suspend(Guid legalEntityId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new SuspendLegalEntityCommand(legalEntityId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPatch("{legalEntityId:guid}/archive")]
    [HasPermission("mdm.legal-entities.update")]
    public async Task<IActionResult> Archive(Guid legalEntityId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ArchiveLegalEntityCommand(legalEntityId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("{legalEntityId:guid}")]
    [HasPermission("mdm.legal-entities.delete")]
    public async Task<IActionResult> Delete(Guid legalEntityId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new DeleteLegalEntityCommand(legalEntityId), cancellationToken);
        return CreateActionResultInstance(response);
    }
}
