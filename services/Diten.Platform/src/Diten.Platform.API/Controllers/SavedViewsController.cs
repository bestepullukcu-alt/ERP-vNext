using Diten.Platform.Application.Features.SavedViews.Commands;
using Diten.Platform.Application.Features.SavedViews.Queries;
using Diten.Platform.Application.Features.SavedViews.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Diten.Platform.API.Controllers;

[ApiController]
[Route("api/personalization/views")]
[Authorize]
public sealed class SavedViewsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SavedViewsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetViews([FromQuery] string moduleKey, [FromQuery] string pageKey)
    {
        var result = await _mediator.Send(new GetSavedViewsQuery(moduleKey, pageKey));
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateSavedViewRequest request)
    {
        var result = await _mediator.Send(new CreateSavedViewCommand(
            request.ModuleKey,
            request.PageKey,
            request.ViewName,
            request.ViewDefinition.ValueKind == JsonValueKind.Undefined ? "{}" : request.ViewDefinition.GetRawText(),
            request.IsDefault,
            string.IsNullOrWhiteSpace(request.Visibility) ? "private" : request.Visibility.Trim().ToLowerInvariant()));

        return CreatedAtAction(nameof(GetViews), new { moduleKey = result.ModuleKey, pageKey = result.PageKey }, result);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateSavedViewRequest request)
    {
        var result = await _mediator.Send(new UpdateSavedViewCommand(
            id,
            request.ModuleKey,
            request.PageKey,
            request.ViewName,
            request.ViewDefinition?.GetRawText(),
            request.IsDefault,
            request.Visibility?.Trim().ToLowerInvariant()));

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await _mediator.Send(new DeleteSavedViewCommand(id));
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}
