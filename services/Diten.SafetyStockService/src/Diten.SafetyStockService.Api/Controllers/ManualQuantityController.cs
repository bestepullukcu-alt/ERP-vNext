using Diten.SafetyStockService.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.SafetyStockService.Api.Controllers;

public sealed record CreateManualQuantityRequest(Guid PolicyRevisionId, Guid SkuId,
    Guid WarehouseId, decimal Quantity, DateOnly EffectiveFrom, DateOnly EffectiveUntil,
    string Reason);
public sealed record ManualQuantityTransitionRequest(Guid PolicyRevisionId, Guid SkuId,
    Guid WarehouseId, long ExpectedVersion);

[Authorize]
[Route("api/v1/safety-stock/policies")]
public sealed class ManualQuantityController(IMediator mediator) : CustomBaseController
{
    [HttpPost("{policyId:guid}/manual-quantities")]
    [Authorize(Policy = "safety-stock.policies.create")]
    public Task<IActionResult> Create(Guid policyId, [FromBody] CreateManualQuantityRequest request, CancellationToken ct)
        => WriteAsync(ManualQuantityOperation.Create, policyId, request.PolicyRevisionId,
            Guid.Empty, request.SkuId, request.WarehouseId, request.Quantity,
            request.EffectiveFrom, request.EffectiveUntil, request.Reason, null, ct);

    [HttpPost("{policyId:guid}/manual-quantities/{quantityId:guid}/submit")]
    [Authorize(Policy = "safety-stock.policies.submit")]
    public Task<IActionResult> Submit(Guid policyId, Guid quantityId,
        [FromBody] ManualQuantityTransitionRequest request, CancellationToken ct)
        => WriteAsync(ManualQuantityOperation.Submit, policyId, request.PolicyRevisionId,
            quantityId, request.SkuId, request.WarehouseId, null, null, null, null,
            request.ExpectedVersion, ct);

    [HttpPost("{policyId:guid}/manual-quantities/{quantityId:guid}/approve")]
    [Authorize(Policy = "safety-stock.policies.review")]
    public Task<IActionResult> Approve(Guid policyId, Guid quantityId,
        [FromBody] ManualQuantityTransitionRequest request, CancellationToken ct)
        => WriteAsync(ManualQuantityOperation.Approve, policyId, request.PolicyRevisionId,
            quantityId, request.SkuId, request.WarehouseId, null, null, null, null,
            request.ExpectedVersion, ct);

    [HttpGet("manual-quantities/effective")]
    [Authorize(Policy = "safety-stock.policies.read")]
    public async Task<IActionResult> Effective([FromQuery] Guid skuId, [FromQuery] Guid warehouseId,
        [FromQuery] DateOnly asOf, CancellationToken ct)
    {
        if (!TryScope(out var tenant, out var company, out var actor)) return Forbid();
        var result = await mediator.Send(new EffectiveManualQuantityQuery(tenant, company,
            actor, skuId, warehouseId, asOf), ct);
        if (result.Status == ManualQuantityStatus.Found && result.Value is { } value)
            return Ok(new { data = new {
                value.Quantity, value.BaseUomId, source = "Manual", value.PolicyRevisionId,
                value.EffectiveFrom, value.EffectiveUntil, recordVersion = value.Version
            } });
        return Respond(result);
    }

    private async Task<IActionResult> WriteAsync(ManualQuantityOperation operation, Guid policyId,
        Guid revisionId, Guid quantityId, Guid skuId, Guid warehouseId, decimal? quantity,
        DateOnly? from, DateOnly? until, string? reason, long? version, CancellationToken ct)
    {
        if (!TryScope(out var tenant, out var company, out var actor)) return Forbid();
        var key = Request.Headers["Idempotency-Key"];
        if (key.Count != 1 || string.IsNullOrWhiteSpace(key[0]))
            return UnprocessableEntity(new { error = "Idempotency-Key is required." });
        var result = await mediator.Send(new ManualQuantityCommand(operation, tenant, company,
            actor, policyId, revisionId, quantityId, skuId, warehouseId, quantity,
            from, until, reason, version, key[0]!), ct);
        return Respond(result);
    }

    private IActionResult Respond(ManualQuantityResult result) => result.Status switch
    {
        ManualQuantityStatus.Created => StatusCode(StatusCodes.Status201Created, new { data = result.Value }),
        ManualQuantityStatus.Updated or ManualQuantityStatus.Found => Ok(new { data = result.Value }),
        ManualQuantityStatus.NotFound => NotFound(),
        ManualQuantityStatus.Conflict => Conflict(new { error = "Quantity state or period conflicts." }),
        ManualQuantityStatus.Invalid => UnprocessableEntity(new { error = "Invalid manual quantity request." }),
        _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Quantity authority is unavailable." })
    };

    private bool TryScope(out Guid tenant, out Guid company, out Guid actor)
    {
        tenant = Claim("tenant_id");
        actor = Claim("sub");
        var header = Request.Headers["X-Legal-Entity-Id"];
        company = header.Count == 1 && Guid.TryParse(header[0], out var parsed) ? parsed : Guid.Empty;
        return tenant != Guid.Empty && actor != Guid.Empty && company != Guid.Empty;
    }

    private Guid Claim(string type)
    {
        var claims = User.Claims.Where(claim => claim.Type == type).ToArray();
        return claims.Length == 1 && Guid.TryParse(claims[0].Value, out var id) ? id : Guid.Empty;
    }
}
