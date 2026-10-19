using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
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

    // FG FIX1 item 1 (BL-559; MOD-0290 s21.2 route): the audited Draft cancellation. The operation id is the
    // Idempotency-Key; the same key again is a replay (200, isReplay).
    [HttpPost("{id:guid}/cancel-draft")]
    [HasPermission(FinishedGoodDraftCancellationPermissions.CancelDraft)]
    public async Task<IActionResult> CancelDraft(
        Guid id,
        [FromBody] CancelFinishedGoodDraftApiRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request is null || request.ExpectedVersion is null || request.UnmappedFields is { Count: > 0 })
            return CreateActionResultInstance(Response<NoContent>.Fail("FINISHED_GOOD_DRAFT_CANCEL_INVALID", 400));
        if (idempotencyKey is not { Length: 36 } || !Guid.TryParseExact(idempotencyKey, "D", out var operationId)
            || operationId == Guid.Empty)
            return CreateActionResultInstance(Response<NoContent>.Fail("IDEMPOTENCY_KEY_INVALID", 400));
        return CreateActionResultInstance(await _mediator.Send(
            new CancelFinishedGoodDraftCommand(id, request.ExpectedVersion.Value, operationId, request.ReasonCode),
            cancellationToken));
    }

    // FG FIX1 item 2: operator repair of admissions a failed Finished Good creation left on its parent GSKU (the LSKU
    // repair, for this child kind: held ones are never released; a stale unbound code is burned first). No screen.
    [HttpPost("operator/child-admission-repairs")]
    [HasPermission(Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.LskuIdentityLifecyclePermissions.RecoverLifecycleOperation)]
    public async Task<IActionResult> RepairChildAdmissions(
        [FromQuery] int limit = 100,
        [FromQuery] int minimumAgeMinutes = 15,
        CancellationToken cancellationToken = default) =>
        CreateActionResultInstance(await _mediator.Send(
            new Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands.RepairLskuChildAdmissionsCommand(
                limit, minimumAgeMinutes, Diten.MdmService.Domain.ValueObjects.GskuChildIdentityKind.FinishedGood),
            cancellationToken));

    public sealed class CancelFinishedGoodDraftApiRequest
    {
        public int? ExpectedVersion { get; init; }
        public string ReasonCode { get; init; } = string.Empty;

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }
}
