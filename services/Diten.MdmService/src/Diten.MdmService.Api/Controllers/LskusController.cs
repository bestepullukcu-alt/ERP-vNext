using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Infrastructure.Authorization;
using Diten.Shared.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/lskus")]
public sealed class LskusController : CustomBaseController
{
    private const int IdempotencyKeyMaximumLength = 128;
    private readonly IMediator _mediator;

    public LskusController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission("mdm.lskus.read")]
    public async Task<IActionResult> GetAll([FromQuery] GetLskusQuery query, CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission("mdm.lskus.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(new GetLskuByIdQuery(id), cancellationToken));

    [HttpGet("create-options")]
    [HasPermission("mdm.lskus.create")]
    public async Task<IActionResult> GetCreateOptions(
        [FromQuery] GetLskuCreateOptionsQuery query,
        CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(query, cancellationToken));

    [HttpPost("drafts")]
    [HasPermission("mdm.lskus.create")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreateLskuDraftPublicRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request.UnmappedFields is { Count: > 0 })
        {
            return CreateActionResultInstance(Response<LskuDraftPublicResponse>.Fail("UNKNOWN_WRITE_FIELD_FORBIDDEN", 400));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > IdempotencyKeyMaximumLength)
        {
            return CreateActionResultInstance(Response<LskuDraftPublicResponse>.Fail("IDEMPOTENCY_KEY_INVALID", 400));
        }

        var create = await _mediator.Send(
            new CreateLskuDraftCommand(new ProductItemSkuMasterModels.CreateLskuDraftRequest
            {
                GskuId = request.GskuId,
                MarketCode = request.MarketCode,
                IdempotencyKey = idempotencyKey
            }),
            cancellationToken);

        // The foundation deliberately returns provider/reservation evidence. Never expose it from this API surface.
        if (create.StatusCode == 202)
        {
            return CreateActionResultInstance(Response<LskuDraftPublicResponse>.Fail(
                "LSKU_BINDING_RECONCILIATION_REQUIRED", 202));
        }

        if (!create.IsSuccessful || create.Data is null)
        {
            return CreateActionResultInstance(Response<LskuDraftPublicResponse>.Fail(create.Errors, create.StatusCode));
        }

        // GetLskuByIdQuery is the already-approved tenant-safe CQRS projection that owns GskuCanonicalCode.
        var detail = await _mediator.Send(new GetLskuByIdQuery(create.Data.LskuId), cancellationToken);
        if (!detail.IsSuccessful || detail.Data is null)
        {
            return CreateActionResultInstance(Response<LskuDraftPublicResponse>.Fail(detail.Errors, detail.StatusCode));
        }

        var result = new LskuDraftPublicResponse(
            create.Data.LskuId,
            create.Data.CanonicalCode,
            create.Data.GskuId,
            detail.Data.GskuCanonicalCode,
            create.Data.MarketCode,
            create.Data.LifecycleStatus,
            create.Data.Version);
        return CreateActionResultInstance(Response<LskuDraftPublicResponse>.Success(result, 201));
    }

    public sealed class CreateLskuDraftPublicRequest
    {
        public Guid GskuId { get; init; }
        public string MarketCode { get; init; } = string.Empty;

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed record LskuDraftPublicResponse(
        Guid LskuId,
        string CanonicalCode,
        Guid GskuId,
        string GskuCanonicalCode,
        string MarketCode,
        Diten.MdmService.Domain.Enums.ProductIdentityLifecycleStatus LifecycleStatus,
        int Version);
}
