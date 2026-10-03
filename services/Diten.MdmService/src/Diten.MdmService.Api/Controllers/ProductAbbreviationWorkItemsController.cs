using Diten.MdmService.Api.Contracts.ProductAbbreviationWorkItems;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Queries;
using Diten.MdmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/work-items/product-abbreviations")]
public sealed class ProductAbbreviationWorkItemsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductAbbreviationWorkItemsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("projection")]
    [HasPermission(ProductAbbreviationPermissions.Read)]
    public async Task<IActionResult> GetProjection(
        [FromQuery] string scope = "self",
        CancellationToken cancellationToken = default)
    {
        if (scope is not ("self" or "team"))
        {
            return Failure<ProductAbbreviationWorkItemProjectionResponse>(400, "WORK_ITEM_SCOPE_INVALID");
        }

        var result = await _mediator.Send(
            new GetProductAbbreviationWorkItemsQuery(
                scope,
                ProductAbbreviationWorkItemContract.MaximumItems),
            cancellationToken);
        return Result(result);
    }

    [HttpPost("{itemId:guid}/actions/{actionCode}")]
    public async Task<IActionResult> DispatchAction(
        Guid itemId,
        string actionCode,
        [FromBody] ProductAbbreviationWorkItemActionRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? headerIdempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        if (request is null
            || request.Payload is null
            || request.HasUnmappedFields
            || !string.Equals(
                request.ProviderCode,
                ProductAbbreviationWorkItemContract.ProviderCode,
                StringComparison.Ordinal)
            || !ProductAbbreviationWorkItemContract.ActionCodes.Contains(actionCode)
            || request.Payload.ExpectedVersion is null or < 0
            || actionCode == "reject"
               && string.IsNullOrWhiteSpace(request.Payload.Reason ?? request.Payload.Note))
        {
            return Failure<ProductAbbreviationWorkItemActionResponse>(
                400,
                "WORK_ITEM_ACTION_PAYLOAD_INVALID");
        }

        // FIX1 C1: one key per command. The body's key (what Platform forwards from the Task Center) and a header key
        // may both be present only when they are the same key.
        var payloadKey = string.IsNullOrEmpty(request.Payload.IdempotencyKey) ? null : request.Payload.IdempotencyKey;
        var headerKey = string.IsNullOrEmpty(headerIdempotencyKey) ? null : headerIdempotencyKey;
        if (payloadKey is not null && headerKey is not null
            && !string.Equals(payloadKey, headerKey, StringComparison.Ordinal))
        {
            return Failure<ProductAbbreviationWorkItemActionResponse>(400, "WORK_ITEM_IDEMPOTENCY_KEY_CONFLICT");
        }

        var result = await _mediator.Send(
            new DispatchProductAbbreviationWorkItemActionCommand(
                itemId,
                actionCode,
                request.ProviderCode,
                request.Payload.ExpectedVersion,
                request.Payload.Reason,
                request.Payload.Note,
                request.HasUnmappedFields,
                payloadKey ?? headerKey),
            cancellationToken);
        return Result(result);
    }

    private ObjectResult Result<T>(ProductAbbreviationWorkItemOperationResult<T> result)
        => StatusCode(
            result.StatusCode,
            ProductAbbreviationWorkItemEnvelopeFactory.From(result));

    private ObjectResult Failure<T>(int statusCode, string reasonCode)
        => StatusCode(
            statusCode,
            ProductAbbreviationWorkItemEnvelope<T>.Fail(statusCode, reasonCode));
}
