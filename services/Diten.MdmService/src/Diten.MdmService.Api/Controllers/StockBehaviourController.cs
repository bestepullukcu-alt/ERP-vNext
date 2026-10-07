using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.StockBehaviour;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Infrastructure.Authorization;
using Diten.Shared.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

/// <summary>
/// WP-MDM-ITEM-MASTER (MOD-0290-FU04) S3 — the stock-behaviour edits of GSKU, LSKU and Finished Good, apart from the
/// identity forms (O-12: own command, own permission constant, own screen in S5). Every edit carries the version it was
/// read at and an Idempotency-Key (the operation id); a field left out is not changed.
/// </summary>
[Authorize]
[ApiController]
public sealed class StockBehaviourController : CustomBaseController
{
    private readonly IMediator _mediator;

    public StockBehaviourController(IMediator mediator) => _mediator = mediator;

    [HttpPut("api/gskus/{id:guid}/stock-behaviour")]
    [HasPermission(StockBehaviourPermissions.EditGsku)]
    public async Task<IActionResult> SetGsku(
        Guid id,
        [FromBody] GskuStockBehaviourRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request is null || request.ExpectedVersion is null || request.UnmappedFields is { Count: > 0 })
            return InvalidRequest();
        if (!TryParseOperationId(idempotencyKey, out var operationId)) return InvalidIdempotencyKey();
        return CreateActionResultInstance(await _mediator.Send(new SetGskuStockBehaviourCommand(
            id, request.ExpectedVersion.Value, operationId, request.BaseUomCode, request.UomConversions,
            request.ShelfLifeDays, request.StorageConditionCodes, request.LotControlled, request.Stockable),
            cancellationToken));
    }

    [HttpPut("api/lskus/{id:guid}/stock-behaviour")]
    [HasPermission(StockBehaviourPermissions.EditLsku)]
    public async Task<IActionResult> SetLsku(
        Guid id,
        [FromBody] LskuStockBehaviourRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request is null || request.ExpectedVersion is null || request.UnmappedFields is { Count: > 0 })
            return InvalidRequest();
        if (!TryParseOperationId(idempotencyKey, out var operationId)) return InvalidIdempotencyKey();
        return CreateActionResultInstance(await _mediator.Send(new SetLskuStockBehaviourCommand(
            id, request.ExpectedVersion.Value, operationId, request.SerialControlled), cancellationToken));
    }

    [HttpPut("api/finished-goods/{id:guid}/stock-behaviour")]
    [HasPermission(StockBehaviourPermissions.EditFinishedGood)]
    public async Task<IActionResult> SetFinishedGood(
        Guid id,
        [FromBody] FinishedGoodStockBehaviourRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request is null || request.ExpectedVersion is null || request.UnmappedFields is { Count: > 0 })
            return InvalidRequest();
        if (!TryParseOperationId(idempotencyKey, out var operationId)) return InvalidIdempotencyKey();
        return CreateActionResultInstance(await _mediator.Send(new SetFinishedGoodStockBehaviourCommand(
            id, request.ExpectedVersion.Value, operationId, request.UomConversions, request.ShelfLifeDays,
            request.StorageConditionCodes, request.LotControlled, request.SerialControlled, request.Stockable),
            cancellationToken));
    }

    [HttpPut("api/finished-goods/{id:guid}/gtin")]
    [HasPermission(StockBehaviourPermissions.EditFinishedGood)]
    public async Task<IActionResult> AssignFinishedGoodGtin(
        Guid id,
        [FromBody] FinishedGoodGtinRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request is null || request.ExpectedVersion is null || request.Gtin is null
            || request.UnmappedFields is { Count: > 0 })
            return InvalidRequest();
        if (!TryParseOperationId(idempotencyKey, out var operationId)) return InvalidIdempotencyKey();
        return CreateActionResultInstance(await _mediator.Send(new AssignFinishedGoodGtinCommand(
            id, request.ExpectedVersion.Value, operationId, request.Gtin), cancellationToken));
    }

    private static bool TryParseOperationId(string? value, out Guid operationId)
    {
        operationId = Guid.Empty;
        return value is { Length: 36 }
            && Guid.TryParseExact(value, "D", out operationId)
            && operationId != Guid.Empty
            && string.Equals(value, operationId.ToString("D"), StringComparison.OrdinalIgnoreCase);
    }

    private IActionResult InvalidRequest() =>
        CreateActionResultInstance(Response<NoContent>.Fail(StockBehaviourErrors.Invalid, 400));

    private IActionResult InvalidIdempotencyKey() =>
        CreateActionResultInstance(Response<NoContent>.Fail("IDEMPOTENCY_KEY_INVALID", 400));

    public sealed class GskuStockBehaviourRequest
    {
        public int? ExpectedVersion { get; init; }
        public string? BaseUomCode { get; init; }
        public List<ItemUomConversion>? UomConversions { get; init; }
        public int? ShelfLifeDays { get; init; }
        public List<string>? StorageConditionCodes { get; init; }
        public bool? LotControlled { get; init; }
        public bool? Stockable { get; init; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed class LskuStockBehaviourRequest
    {
        public int? ExpectedVersion { get; init; }
        public bool? SerialControlled { get; init; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed class FinishedGoodStockBehaviourRequest
    {
        public int? ExpectedVersion { get; init; }
        public List<ItemUomConversion>? UomConversions { get; init; }
        public int? ShelfLifeDays { get; init; }
        public List<string>? StorageConditionCodes { get; init; }
        public bool? LotControlled { get; init; }
        public bool? SerialControlled { get; init; }
        public bool? Stockable { get; init; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed class FinishedGoodGtinRequest
    {
        public int? ExpectedVersion { get; init; }
        public string? Gtin { get; init; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }
}
