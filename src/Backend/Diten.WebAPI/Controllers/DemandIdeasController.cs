using Asp.Versioning;
using Diten.Application.Commands.DemandIdeaCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.DemandIdeas;
using Diten.Application.Queries.DemandIdeaQueries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/demand-ideas")]
public sealed class DemandIdeasController : ControllerBase
{
    private readonly IMediator _mediator;

    public DemandIdeasController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("meta")]
    public async Task<ActionResult<DemandIdeaMetadataDto>> GetMetadata(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDemandIdeaMetadataQuery(), ct);
        return result.Success ? Ok(result.Data) : MapError(result, "Could not load metadata.");
    }

    [HttpGet("related")]
    public async Task<ActionResult<IReadOnlyList<RelatedIdeaItemDto>>> GetRelated([FromQuery] RelatedQuery query, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRelatedDemandIdeasQuery
        {
            Title = query.Title,
            RequestType = query.RequestType,
            BusinessUnit = query.BusinessUnit,
            StrategicAlignment = query.StrategicAlignment,
            Tags = query.Tags,
            ExcludeId = query.ExcludeId,
            Take = query.Take
        }, ct);

        return result.Success ? Ok(result.Data) : MapError(result, "Could not load related ideas.");
    }

    [HttpPost("check-duplicates")]
    public async Task<ActionResult<IReadOnlyList<DuplicateIdeaItemDto>>> CheckDuplicates([FromBody] DuplicateCheckRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CheckDemandIdeaDuplicatesQuery
        {
            Title = request.Title,
            RequestType = request.RequestType,
            BusinessUnit = request.BusinessUnit,
            Tags = request.Tags,
            ExcludeId = request.ExcludeId
        }, ct);

        return result.Success ? Ok(result.Data) : MapError(result, "Could not check duplicates.");
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DemandIdeaResponseDto>>> List(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListDemandIdeasQuery(), ct);
        return result.Success ? Ok(result.Data) : MapError(result, "Could not list demand ideas.");
    }

    [HttpPost]
    public async Task<ActionResult<DemandIdeaResponseDto>> Create([FromBody] DemandIdeaUpsertRequest? body, CancellationToken ct)
    {
        body ??= new DemandIdeaUpsertRequest();
        var result = await _mediator.Send(new CreateDemandIdeaDraftCommand
        {
            Request = body,
            UserId = User?.Identity?.Name
        }, ct);

        if (!result.Success)
            return MapError(result, "Could not create draft.");

        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DemandIdeaResponseDto>> GetById(string id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDemandIdeaByIdQuery { Id = id }, ct);
        return result.Success ? Ok(result.Data) : MapError(result, "Not found.");
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] DemandIdeaUpsertRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateDemandIdeaCommand
        {
            Id = id,
            Request = body,
            UserId = User?.Identity?.Name
        }, ct);

        return result.Success ? Ok(result.Data) : MapError(result, "Update failed.");
    }

    [HttpPost("{id}/submit")]
    public async Task<IActionResult> Submit(string id, CancellationToken ct)
    {
        var result = await _mediator.Send(new SubmitDemandIdeaCommand
        {
            Id = id,
            UserId = User?.Identity?.Name
        }, ct);

        return result.Success ? Ok(result.Data) : MapError(result, "Submit failed.");
    }

    private ActionResult MapError<T>(Response<T> result, string defaultMessage)
    {
        var payload = new ApiErrorEnvelope
        {
            Success = false,
            Message = defaultMessage,
            Errors = result.Error?.Details.Count > 0 ? result.Error?.Details : null,
            ErrorCode = result.Error?.Code
        };

        return result.Error?.Code switch
        {
            ResultErrorCodes.NotFound => NotFound(payload),
            ResultErrorCodes.Conflict => Conflict(payload),
            _ => BadRequest(payload)
        };
    }
}
