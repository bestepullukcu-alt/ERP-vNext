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
[Route("api/gskus")]
public sealed class GskusController : CustomBaseController
{
    private readonly IMediator _mediator;

    public GskusController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission("mdm.gskus.read")]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetGskusQuery query,
        CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission("mdm.gskus.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(new GetGskuByIdQuery(id), cancellationToken));

    [HttpGet("create-options")]
    [HasPermission("mdm.gskus.create")]
    public async Task<IActionResult> GetCreateOptions(
        [FromQuery] GetGskuCreateOptionsQuery query,
        CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(query, cancellationToken));

    [HttpPost("drafts")]
    [HasPermission("mdm.gskus.create")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] ProductItemSkuMasterModels.CreateFirstGskuDraftFacadeRequest request,
        [FromHeader(Name = "Idempotency-Key")] string operationId,
        CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(
            new CreateFirstGskuDraftFacadeCommand(request, operationId),
            cancellationToken));

    [HttpPost("{id:guid}/submit")]
    [HasPermission(FirstGskuIdentityLifecyclePermissions.Submit)]
    public async Task<IActionResult> SubmitIdentity(
        Guid id,
        [FromBody] SubmitGskuIdentityApiRequest request,
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
            new StartFirstGskuIdentityWorkflowCommand(
                new(id, request.ExpectedVersion.Value, operationId)),
            cancellationToken));
    }

    [HttpPost("{id:guid}/retire")]
    [HasPermission(GskuPairRetirementPermissions.Retire)]
    public async Task<IActionResult> RetireIdentity(
        Guid id,
        [FromBody] RetireGskuIdentityApiRequest request,
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
            new RetireGskuIdentityPairCommand(
                new(id, request.ExpectedVersion.Value, operationId, request.ReasonCode)),
            cancellationToken));
    }

    private static bool TryParseOperationId(string? value, out Guid operationId) =>
        Guid.TryParseExact(value, "D", out operationId) && operationId != Guid.Empty;

    private static bool HasUnknownFields(IDictionary<string, JsonElement>? fields) => fields is { Count: > 0 };

    private IActionResult InvalidLifecycleRequest() =>
        CreateActionResultInstance(Response<NoContent>.Fail("GSKU_IDENTITY_LIFECYCLE_REQUEST_INVALID", 400));

    private IActionResult InvalidIdempotencyKey() =>
        CreateActionResultInstance(Response<NoContent>.Fail("IDEMPOTENCY_KEY_INVALID", 400));

    public sealed class SubmitGskuIdentityApiRequest
    {
        public int? ExpectedVersion { get; init; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed class RetireGskuIdentityApiRequest
    {
        public int? ExpectedVersion { get; init; }
        public string ReasonCode { get; init; } = string.Empty;

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }
}
