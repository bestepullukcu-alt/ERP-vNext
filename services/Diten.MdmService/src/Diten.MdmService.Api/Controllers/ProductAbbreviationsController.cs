using Diten.MdmService.Api.Contracts.ProductAbbreviations;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Queries;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/product-abbreviations")]
public sealed class ProductAbbreviationsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ProductAbbreviationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("by-global-product/{globalProductId:guid}")]
    [HasPermission(ProductAbbreviationPermissions.Read)]
    public async Task<IActionResult> GetByGlobalProduct(
        Guid globalProductId,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetProductAbbreviationByGlobalProductQuery(globalProductId),
            cancellationToken));

    [HttpGet("resolve/{abbreviation}")]
    [HasPermission(ProductAbbreviationPermissions.Read)]
    public async Task<IActionResult> Resolve(string abbreviation, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ResolveProductAbbreviationQuery(abbreviation),
            cancellationToken));

    [HttpGet("{registerEntryId:guid}/evidence")]
    [HasPermission(ProductAbbreviationPermissions.Audit)]
    public async Task<IActionResult> GetEvidence(Guid registerEntryId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetProductAbbreviationAllocationEvidenceQuery(registerEntryId),
            cancellationToken));

    [HttpPost("requests")]
    [HasPermission(ProductAbbreviationPermissions.Request)]
    public async Task<IActionResult> RequestAllocation(
        [FromBody] RequestAllocationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new RequestProductAbbreviationAllocationCommand(
                request.GlobalProductId,
                request.Abbreviation,
                idempotencyKey),
            cancellationToken));

    [HttpPatch("{registerEntryId:guid}/cancel")]
    [HasPermission(ProductAbbreviationPermissions.Cancel)]
    public async Task<IActionResult> Cancel(
        Guid registerEntryId,
        [FromBody] CancelAllocationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CancelProductAbbreviationAllocationCommand(
                registerEntryId,
                request.ExpectedVersion,
                idempotencyKey,
                request.Reason),
            cancellationToken));

    [HttpPatch("{registerEntryId:guid}/approve")]
    [HasPermission(ProductAbbreviationPermissions.Approve)]
    public async Task<IActionResult> Approve(
        Guid registerEntryId,
        [FromBody] ApproveAllocationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ApproveProductAbbreviationAllocationCommand(
                registerEntryId,
                request.ExpectedVersion,
                idempotencyKey,
                request.ExpectedFormerVersion,
                request.Reason),
            cancellationToken));

    [HttpPatch("{registerEntryId:guid}/reject")]
    [HasPermission(ProductAbbreviationPermissions.Reject)]
    public async Task<IActionResult> Reject(
        Guid registerEntryId,
        [FromBody] RejectAllocationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new RejectProductAbbreviationAllocationCommand(
                registerEntryId,
                request.ExpectedVersion,
                idempotencyKey,
                request.Reason),
            cancellationToken));

    [HttpPost("{registerEntryId:guid}/corrections")]
    [HasPermission(ProductAbbreviationPermissions.Correct)]
    public async Task<IActionResult> InitiateCorrection(
        Guid registerEntryId,
        [FromBody] InitiateCorrectionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new InitiateProductAbbreviationCorrectionCommand(
                registerEntryId,
                request.ExpectedVersion,
                request.ReplacementAbbreviation,
                idempotencyKey,
                request.Reason),
            cancellationToken));

    [HttpPost("{registerEntryId:guid}/retirement-requests")]
    [HasPermission(ProductAbbreviationPermissions.Retire)]
    public async Task<IActionResult> RequestRetirement(
        Guid registerEntryId,
        [FromBody] RequestRetirementRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new RequestProductAbbreviationRetirementCommand(
                registerEntryId,
                request.ExpectedVersion,
                idempotencyKey,
                request.Reason),
            cancellationToken));

    [HttpPatch("{registerEntryId:guid}/retirement-requests/{retirementRequestId}/approve")]
    [HasPermission(ProductAbbreviationPermissions.Approve)]
    public async Task<IActionResult> ApproveRetirement(
        Guid registerEntryId,
        string retirementRequestId,
        [FromBody] ApproveRetirementRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ApproveProductAbbreviationRetirementCommand(
                registerEntryId,
                request.ExpectedVersion,
                retirementRequestId,
                idempotencyKey,
                request.Reason),
            cancellationToken));

    [HttpPatch("{registerEntryId:guid}/retirement-requests/{retirementRequestId}/reject")]
    [HasPermission(ProductAbbreviationPermissions.Reject)]
    public async Task<IActionResult> RejectRetirement(
        Guid registerEntryId,
        string retirementRequestId,
        [FromBody] RejectRetirementRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new RejectProductAbbreviationRetirementCommand(
                registerEntryId,
                request.ExpectedVersion,
                retirementRequestId,
                idempotencyKey,
                request.Reason),
            cancellationToken));
}
