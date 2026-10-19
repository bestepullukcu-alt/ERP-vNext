using System.Security.Claims;
using Diten.PlanningService.Api.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.Mrp;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PlanningService.Api.Features.Mrp;

public sealed record LeadTimeCreateRequest(Guid SkuId, Guid WarehouseId, int Days,
    DateOnly EffectiveFrom, DateOnly? EffectiveUntil);
public sealed record LeadTimeReviseRequest(Guid SkuId, Guid WarehouseId, Guid RevisionId,
    long ExpectedVersion, int Days, DateOnly EffectiveFrom, DateOnly? EffectiveUntil);
public sealed record LeadTimeTransitionRequest(Guid SkuId, Guid WarehouseId,
    long ExpectedVersion, string? Reason);

[Authorize]
[Route("api/v1/mrp/lead-times")]
public sealed class LeadTimeController(ISender sender) : CustomBaseController
{
    [HttpPost]
    [HasPermission("mrp.lead-times.create")]
    public Task<IActionResult> Create([FromBody] LeadTimeCreateRequest request, CancellationToken ct) =>
        SendAsync(LeadTimeOperation.Create, Guid.Empty, Guid.Empty, request.SkuId,
            request.WarehouseId, request.Days, request.EffectiveFrom, request.EffectiveUntil,
            null, null, ct);

    [HttpPost("{policyId:guid}/revisions")]
    [HasPermission("mrp.lead-times.update")]
    public Task<IActionResult> Revise(Guid policyId, [FromBody] LeadTimeReviseRequest request,
        CancellationToken ct) =>
        SendAsync(LeadTimeOperation.Revise, policyId, request.RevisionId, request.SkuId,
            request.WarehouseId, request.Days, request.EffectiveFrom, request.EffectiveUntil,
            request.ExpectedVersion, null, ct);

    [HttpPost("{policyId:guid}/revisions/{revisionId:guid}/submit")]
    [HasPermission("mrp.lead-times.submit")]
    public Task<IActionResult> Submit(Guid policyId, Guid revisionId,
        [FromBody] LeadTimeTransitionRequest request, CancellationToken ct) =>
        SendAsync(LeadTimeOperation.Submit, policyId, revisionId, request.SkuId,
            request.WarehouseId, null, null, null, request.ExpectedVersion, null, ct);

    [HttpPost("{policyId:guid}/revisions/{revisionId:guid}/approve")]
    [HasPermission("mrp.lead-times.review")]
    public Task<IActionResult> Approve(Guid policyId, Guid revisionId,
        [FromBody] LeadTimeTransitionRequest request, CancellationToken ct) =>
        SendAsync(LeadTimeOperation.Approve, policyId, revisionId, request.SkuId,
            request.WarehouseId, null, null, null, request.ExpectedVersion, null, ct);

    [HttpPost("{policyId:guid}/revisions/{revisionId:guid}/reject")]
    [HasPermission("mrp.lead-times.review")]
    public Task<IActionResult> Reject(Guid policyId, Guid revisionId,
        [FromBody] LeadTimeTransitionRequest request, CancellationToken ct) =>
        SendAsync(LeadTimeOperation.Reject, policyId, revisionId, request.SkuId,
            request.WarehouseId, null, null, null, request.ExpectedVersion, request.Reason, ct);

    private async Task<IActionResult> SendAsync(LeadTimeOperation operation, Guid policyId,
        Guid revisionId, Guid skuId, Guid warehouseId, int? days, DateOnly? from,
        DateOnly? until, long? version, string? reason, CancellationToken ct)
    {
        var tenant = HttpContext.Items["DemandPlanning.TenantId"] is Guid value ? value : Guid.Empty;
        var actorClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (tenant == Guid.Empty || !Guid.TryParse(actorClaim, out var actor) || actor == Guid.Empty ||
            !Guid.TryParse(Request.Headers["X-Legal-Entity-Id"].ToString(), out var company) ||
            company == Guid.Empty) return Forbid();
        var key = Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key)) return BadRequest(new { error = "Idempotency-Key is required." });
        var result = await sender.Send(new LeadTimeCommand(operation, tenant, company, skuId,
            warehouseId, actor, policyId, revisionId, days, from, until, version, reason, key), ct);
        return result.Status switch
        {
            LeadTimeStatus.Created => StatusCode(201, new { data = result.Receipt }),
            LeadTimeStatus.Updated => Ok(new { data = result.Receipt }),
            LeadTimeStatus.NotFound => NotFound(),
            LeadTimeStatus.Forbidden => Forbid(),
            LeadTimeStatus.Conflict => Conflict(),
            LeadTimeStatus.Invalid => UnprocessableEntity(),
            _ => StatusCode(503, new { error = "Lead-time source or audit is unavailable." })
        };
    }
}
