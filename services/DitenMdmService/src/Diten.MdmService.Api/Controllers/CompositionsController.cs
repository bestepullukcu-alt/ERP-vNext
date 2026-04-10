using Diten.MdmService.Application.Features.Compositions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/compositions")]
public sealed class CompositionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompositionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetAllCompositionsQuery());
        return Ok(new { data = result });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid? versionId = null)
    {
        var result = await _mediator.Send(new GetCompositionByIdQuery(id, versionId));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid id)
    {
        var result = await _mediator.Send(new GetCompositionVersionHistoryQuery(id));
        return Ok(new { data = result });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCompositionCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCompositionCommand command)
    {
        command.Id = id;
        var updated = await _mediator.Send(command);
        return updated ? NoContent() : NotFound();
    }

    [HttpPatch("versions/{versionId:guid}/activate")]
    public async Task<IActionResult> ActivateVersion(Guid versionId)
    {
        var result = await _mediator.Send(new ActivateCompositionVersionCommand { VersionId = versionId });
        return result ? NoContent() : BadRequest();
    }

    [HttpPatch("{id:guid}/lifecycle")]
    public async Task<IActionResult> ChangeLifecycle(Guid id, [FromBody] ChangeCompositionLifecycleCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest();
        }

        var updated = await _mediator.Send(command);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _mediator.Send(new DeleteCompositionCommand(id));
        return deleted ? NoContent() : NotFound();
    }
}
