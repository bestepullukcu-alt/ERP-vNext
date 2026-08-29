using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.MdmService.Infrastructure.Authorization;
using Diten.Shared.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/global-products")]
public sealed class GlobalProductsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public GlobalProductsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission("mdm.global-products.read")]
    public async Task<IActionResult> GetAll([FromQuery] GetGlobalProductsQuery query, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(query, cancellationToken));

    [HttpGet("selector")]
    [HasPermission("mdm.global-products.read")]
    public async Task<IActionResult> GetSelector(
        [FromQuery] GetGlobalProductSelectorQuery query,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission("mdm.global-products.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetGlobalProductByIdQuery(id), cancellationToken));

    [HttpPost("code-reservations")]
    [HasPermission("mdm.global-products.create")]
    public async Task<IActionResult> ReserveCode(
        [FromBody] ProductItemSkuMasterModels.ReserveGlobalProductCodeRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ReserveCanonicalCodeCommand(request), cancellationToken));

    [HttpPost("drafts")]
    [HasPermission("mdm.global-products.create")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] ProductItemSkuMasterModels.CreateGlobalProductDraftRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new CreateGlobalProductDraftCommand(request), cancellationToken));

    [HttpPost("{id:guid}/submit")]
    [HasPermission(ProductIdentityLifecyclePermissions.GlobalProductSubmit)]
    public async Task<IActionResult> SubmitIdentity(
        Guid id,
        [FromBody] SubmitGlobalProductIdentityApiRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request is null || HasUnknownFields(request.UnmappedFields))
        {
            return InvalidLifecycleRequest();
        }
        if (!TryParseOperationId(idempotencyKey, out var operationId))
        {
            return InvalidIdempotencyKey();
        }

        return CreateActionResultInstance(await _mediator.Send(
            new StartGlobalProductIdentityWorkflowCommand(
                new(id, request.ExpectedVersion, operationId)),
            cancellationToken));
    }

    [HttpPost("{id:guid}/retire")]
    [HasPermission(ProductIdentityLifecyclePermissions.GlobalProductRetire)]
    public async Task<IActionResult> RetireIdentity(
        Guid id,
        [FromBody] RetireGlobalProductIdentityApiRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request is null || HasUnknownFields(request.UnmappedFields))
        {
            return InvalidLifecycleRequest();
        }
        if (!TryParseOperationId(idempotencyKey, out var operationId))
        {
            return InvalidIdempotencyKey();
        }

        return CreateActionResultInstance(await _mediator.Send(
            new RetireGlobalProductIdentityCommand(
                new(id, request.ExpectedVersion, operationId, request.ReasonCode, request.Comment)),
            cancellationToken));
    }

    private static bool TryParseOperationId(string? value, out Guid operationId) =>
        Guid.TryParseExact(value, "D", out operationId) && operationId != Guid.Empty;

    private static bool HasUnknownFields(IDictionary<string, JsonElement>? fields) =>
        fields is { Count: > 0 };

    private IActionResult InvalidLifecycleRequest() =>
        CreateActionResultInstance(Response<NoContent>.Fail("PRODUCT_IDENTITY_LIFECYCLE_REQUEST_INVALID", 400));

    private IActionResult InvalidIdempotencyKey() =>
        CreateActionResultInstance(Response<NoContent>.Fail("IDEMPOTENCY_KEY_INVALID", 400));

    public sealed class SubmitGlobalProductIdentityApiRequest
    {
        public int ExpectedVersion { get; init; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed class RetireGlobalProductIdentityApiRequest
    {
        public int ExpectedVersion { get; init; }
        public string ReasonCode { get; init; } = string.Empty;
        public string? Comment { get; init; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }
}
