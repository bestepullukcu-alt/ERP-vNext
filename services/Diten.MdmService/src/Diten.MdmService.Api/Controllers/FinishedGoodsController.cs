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
[Route("api/finished-goods")]
public sealed class FinishedGoodsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public FinishedGoodsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission("mdm.finished-goods.read")]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetFinishedGoodsQuery query,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission("mdm.finished-goods.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetFinishedGoodByIdQuery(id), cancellationToken));

    [HttpGet("gsku-selector")]
    [HasPermission("mdm.finished-goods.create")]
    public async Task<IActionResult> GetGskuSelector(
        [FromQuery] GetFinishedGoodGskuSelectorQuery query,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(query, cancellationToken));

    [HttpPost("drafts")]
    [HasPermission("mdm.finished-goods.create")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] ProductItemSkuMasterModels.CreateFinishedGoodDraftRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new CreateFinishedGoodDraftCommand(request), cancellationToken));

    [HttpPost("{id:guid}/submit")]
    [HasPermission(FinishedGoodIdentityLifecyclePermissions.Submit)]
    public async Task<IActionResult> SubmitIdentity(
        Guid id,
        [FromBody] SubmitFinishedGoodIdentityApiRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request is null || request.ExpectedVersion is null || HasUnknownFields(request.UnmappedFields))
        {
            return InvalidLifecycleRequest();
        }
        if (!TryParseOperationId(idempotencyKey, out var operationId))
        {
            return InvalidIdempotencyKey();
        }

        return CreateActionResultInstance(await _mediator.Send(
            new StartFinishedGoodIdentityWorkflowCommand(
                new(id, request.ExpectedVersion.Value, operationId)),
            cancellationToken));
    }

    [HttpPost("{id:guid}/retire")]
    [HasPermission(FinishedGoodIdentityLifecyclePermissions.Retire)]
    public async Task<IActionResult> RetireIdentity(
        Guid id,
        [FromBody] RetireFinishedGoodIdentityApiRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request is null || request.ExpectedVersion is null || HasUnknownFields(request.UnmappedFields))
        {
            return InvalidLifecycleRequest();
        }
        if (!TryParseOperationId(idempotencyKey, out var operationId))
        {
            return InvalidIdempotencyKey();
        }

        return CreateActionResultInstance(await _mediator.Send(
            new RetireFinishedGoodIdentityCommand(
                new(id, request.ExpectedVersion.Value, operationId, request.ReasonCode, request.Comment)),
            cancellationToken));
    }

    private static bool TryParseOperationId(string? value, out Guid operationId)
    {
        operationId = Guid.Empty;
        return value is { Length: 36 }
            && Guid.TryParseExact(value, "D", out operationId)
            && operationId != Guid.Empty
            && string.Equals(value, operationId.ToString("D"), StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasUnknownFields(IDictionary<string, JsonElement>? fields) => fields is { Count: > 0 };

    private IActionResult InvalidLifecycleRequest() =>
        CreateActionResultInstance(Response<NoContent>.Fail("FINISHED_GOOD_IDENTITY_LIFECYCLE_REQUEST_INVALID", 400));

    private IActionResult InvalidIdempotencyKey() =>
        CreateActionResultInstance(Response<NoContent>.Fail("IDEMPOTENCY_KEY_INVALID", 400));

    public sealed class SubmitFinishedGoodIdentityApiRequest
    {
        public int? ExpectedVersion { get; init; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed class RetireFinishedGoodIdentityApiRequest
    {
        public int? ExpectedVersion { get; init; }
        public string ReasonCode { get; init; } = string.Empty;
        public string? Comment { get; init; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }
}
