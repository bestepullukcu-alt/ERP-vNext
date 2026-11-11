using Diten.SafetyStockService.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.SafetyStockService.Api.Controllers;

public sealed record CreatePolicyRequest(Guid SkuId, Guid WarehouseId, int CoverageDays);
public sealed record RevisePolicyRequest(Guid SourceRevisionId, int CoverageDays, long ExpectedVersion, string Reason);
public sealed record PolicyTransitionRequest(long ExpectedVersion, string? Reason);

[Authorize]
[Route("api/v1/safety-stock/policies")]
public sealed class SafetyStockCommandController : CustomBaseController
{
    private readonly IMediator _mediator;

    public SafetyStockCommandController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [Authorize(Policy = "safety-stock.policies.create")]
    public Task<IActionResult> Create([FromBody] CreatePolicyRequest request, CancellationToken ct)
        => SendAsync(PolicyOperation.Create, Guid.Empty, Guid.Empty, request.SkuId, request.WarehouseId,
            request.CoverageDays, null, null, ct);

    [HttpPost("{policyId:guid}/revisions")]
    [Authorize(Policy = "safety-stock.policies.update")]
    public Task<IActionResult> Revise(Guid policyId, [FromBody] RevisePolicyRequest request, CancellationToken ct)
        => SendAsync(PolicyOperation.Revise, policyId, request.SourceRevisionId, Guid.Empty, Guid.Empty,
            request.CoverageDays, request.ExpectedVersion, request.Reason, ct);

    [HttpPost("{policyId:guid}/revisions/{revisionId:guid}/submit")]
    [Authorize(Policy = "safety-stock.policies.submit")]
    public Task<IActionResult> Submit(Guid policyId, Guid revisionId, [FromBody] PolicyTransitionRequest request, CancellationToken ct)
        => SendAsync(PolicyOperation.Submit, policyId, revisionId, Guid.Empty, Guid.Empty,
            null, request.ExpectedVersion, null, ct);

    [HttpPost("{policyId:guid}/revisions/{revisionId:guid}/approve")]
    [Authorize(Policy = "safety-stock.policies.review")]
    public Task<IActionResult> Approve(Guid policyId, Guid revisionId, [FromBody] PolicyTransitionRequest request, CancellationToken ct)
        => SendAsync(PolicyOperation.Approve, policyId, revisionId, Guid.Empty, Guid.Empty,
            null, request.ExpectedVersion, null, ct);

    [HttpPost("{policyId:guid}/revisions/{revisionId:guid}/reject")]
    [Authorize(Policy = "safety-stock.policies.review")]
    public Task<IActionResult> Reject(Guid policyId, Guid revisionId, [FromBody] PolicyTransitionRequest request, CancellationToken ct)
        => SendAsync(PolicyOperation.Reject, policyId, revisionId, Guid.Empty, Guid.Empty,
            null, request.ExpectedVersion, request.Reason, ct);

    private async Task<IActionResult> SendAsync(PolicyOperation operation, Guid policyId, Guid revisionId,
        Guid skuId, Guid warehouseId, int? coverageDays, long? expectedVersion, string? reason, CancellationToken ct)
    {
        var tenant = SingleGuidClaim("tenant_id");
        var actor = SingleGuidClaim("sub");
        var companyHeader = Request.Headers["X-Legal-Entity-Id"];
        if (tenant is null || actor is null || companyHeader.Count != 1
            || !Guid.TryParse(companyHeader[0], out var company) || company == Guid.Empty)
            return StatusCode(StatusCodes.Status403Forbidden);

        var keyHeader = Request.Headers["Idempotency-Key"];
        if (keyHeader.Count != 1 || string.IsNullOrWhiteSpace(keyHeader[0]))
            return UnprocessableEntity(new { error = "Idempotency-Key is required." });

        var result = await _mediator.Send(new PolicyMutationCommand(operation, tenant.Value, company, actor.Value,
            policyId, revisionId, skuId, warehouseId, coverageDays, expectedVersion, reason, keyHeader[0]!), ct);
        return result.Status switch
        {
            PolicyMutationStatus.Created => StatusCode(StatusCodes.Status201Created, new { data = result.Receipt }),
            PolicyMutationStatus.Updated => Ok(new { data = result.Receipt }),
            PolicyMutationStatus.NotFound => NotFound(),
            PolicyMutationStatus.Conflict => Conflict(new { error = "Policy state or version conflicts." }),
            PolicyMutationStatus.Invalid => UnprocessableEntity(new { error = "Invalid policy command." }),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Policy write is unavailable." })
        };
    }

    private Guid? SingleGuidClaim(string type)
    {
        var matches = User.Claims.Where(claim => claim.Type == type).ToArray();
        return matches.Length == 1 && Guid.TryParse(matches[0].Value, out var id) && id != Guid.Empty
            ? id : null;
    }
}
