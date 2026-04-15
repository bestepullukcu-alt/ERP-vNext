using Diten.MdmService.Application.Features.PackagingDefinitions;
using Diten.MdmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PackagingDefinitionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PackagingDefinitionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("Modules.PackagingDefinitions.View")]
    public async Task<ActionResult<IReadOnlyList<PackagingDefinitionListItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllPackagingDefinitionsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("Modules.PackagingDefinitions.View")]
    public async Task<ActionResult<PackagingDefinitionDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPackagingDefinitionByIdQuery(id), cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("Modules.PackagingDefinitions.Create")]
    public async Task<ActionResult<Guid>> Create([FromBody] CreatePackagingDefinitionRequest request, CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(request, cancellationToken);
        return Ok(id);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("Modules.PackagingDefinitions.Edit")]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdatePackagingDefinitionRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("ID mismatch.");
        var success = await _mediator.Send(request, cancellationToken);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("Modules.PackagingDefinitions.Delete")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeletePackagingDefinitionRequest(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("bulk-delete")]
    [HasPermission("Modules.PackagingDefinitions.Delete")]
    public async Task<ActionResult<BulkDeletePackagingDefinitionsResponse>> BulkDelete([FromBody] BulkDeletePackagingDefinitionsRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(request, cancellationToken);
        return Ok(response);
    }

    [HttpPatch("{id:guid}/lifecycle")]
    [HasPermission("Modules.PackagingDefinitions.Edit")]
    public async Task<ActionResult> ChangeLifecycle(Guid id, [FromBody] ChangePackagingDefinitionLifecycleRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("ID mismatch.");
        var success = await _mediator.Send(request, cancellationToken);
        if (!success) return NotFound();
        return NoContent();
    }
}
