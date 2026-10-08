using System.Security.Claims;
using System.Text.Json.Serialization;
using Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PlanningService.Api.Features.DemandPlanning;

// Candidate Demand v2 Draft routes. These do not publish an immutable baseline.
[Route("api/v2/demand/revisions")]
public sealed class ManualDraftRevisionsController(ISender sender) : CustomBaseController
{
    [HttpPost]
    [HasPermission("demand.drafts.create")]
    public async Task<IActionResult> Create([FromBody] CreateManualDraftRequest request,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var tenantId, out var actorId)) return Forbid();
        var result = await sender.Send(new CreateManualDraftCommand(
            tenantId, actorId, legalEntityHint ?? Guid.Empty,
            User.HasClaim("permission", "demand.drafts.create"),
            request.PlanningCycleId, request.Reason, request.Series,
            idempotencyKey ?? string.Empty), cancellationToken);
        return CreateActionResultInstance(result);
    }

    [HttpGet("{revisionId:guid}")]
    [HasPermission("demand.plans.read")]
    public async Task<IActionResult> Get(Guid revisionId,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var tenantId, out var actorId)) return Forbid();
        var result = await sender.Send(new GetManualDraftQuery(
            tenantId, actorId, legalEntityHint ?? Guid.Empty,
            User.HasClaim("permission", "demand.plans.read"),
            revisionId), cancellationToken);
        return CreateActionResultInstance(result);
    }

    [HttpPatch("{revisionId:guid}/manual-weeks/{skuId:guid}/{warehouseId}/{weekNumber:int}")]
    [HasPermission("demand.drafts.update")]
    public async Task<IActionResult> EditWeek(Guid revisionId, Guid skuId,
        string warehouseId, int weekNumber, [FromBody] EditManualWeekRequest request,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var tenantId, out var actorId)) return Forbid();
        var result = await sender.Send(new EditManualDraftWeekCommand(
            tenantId, actorId, legalEntityHint ?? Guid.Empty,
            User.HasClaim("permission", "demand.drafts.update"),
            revisionId, skuId, warehouseId, weekNumber,
            request.ValueKind, request.Quantity, request.Reason,
            idempotencyKey ?? string.Empty, request.ExpectedContentVersion),
            cancellationToken);
        return CreateActionResultInstance(result);
    }

    [HttpPost("{revisionId:guid}/submit-review")]
    [HasPermission("demand.drafts.update")]
    public Task<IActionResult> Submit(Guid revisionId,
        [FromBody] DraftTransitionRequest request,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken) =>
        Transition(revisionId, request, legalEntityHint, idempotencyKey,
            DraftReviewAction.Submitted, "demand.drafts.update", cancellationToken);

    [HttpPost("{revisionId:guid}/approve")]
    [HasPermission("demand.plans.review")]
    public Task<IActionResult> Approve(Guid revisionId,
        [FromBody] DraftTransitionRequest request,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken) =>
        Transition(revisionId, request, legalEntityHint, idempotencyKey,
            DraftReviewAction.Approved, "demand.plans.review", cancellationToken);

    [HttpPost("{revisionId:guid}/reject")]
    [HasPermission("demand.plans.review")]
    public Task<IActionResult> Reject(Guid revisionId,
        [FromBody] DraftTransitionRequest request,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken) =>
        Transition(revisionId, request, legalEntityHint, idempotencyKey,
            DraftReviewAction.Rejected, "demand.plans.review", cancellationToken);

    [HttpPost("{revisionId:guid}/reopen")]
    [HasPermission("demand.drafts.update")]
    public Task<IActionResult> Reopen(Guid revisionId,
        [FromBody] DraftTransitionRequest request,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken) =>
        Transition(revisionId, request, legalEntityHint, idempotencyKey,
            DraftReviewAction.Reopened, "demand.drafts.update", cancellationToken);

    [HttpPost("{revisionId:guid}/excluded-series/{skuId:guid}/{warehouseId}")]
    [HasPermission("demand.drafts.update")]
    public async Task<IActionResult> ExcludeSeries(Guid revisionId, Guid skuId,
        string warehouseId, [FromBody] ExcludeManualDraftSeriesRequest request,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var tenantId, out var actorId)) return Forbid();
        var result = await sender.Send(new ExcludeManualDraftSeriesCommand(
            tenantId, actorId, legalEntityHint ?? Guid.Empty,
            User.HasClaim("permission", "demand.drafts.update"),
            revisionId, skuId, warehouseId, request.Reason,
            idempotencyKey ?? string.Empty, request.ExpectedContentVersion,
            request.ExpectedStateVersion), cancellationToken);
        return CreateActionResultInstance(result);
    }

    private async Task<IActionResult> Transition(Guid revisionId,
        DraftTransitionRequest request, Guid? legalEntityHint, string? idempotencyKey,
        DraftReviewAction action, string permission, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var tenantId, out var actorId)) return Forbid();
        var result = await sender.Send(new TransitionManualDraftCommand(
            tenantId, actorId, legalEntityHint ?? Guid.Empty,
            User.HasClaim("permission", permission), revisionId, action,
            request.Reason, idempotencyKey ?? string.Empty,
            request.ExpectedContentVersion, request.ExpectedStateVersion),
            cancellationToken);
        return CreateActionResultInstance(result);
    }

    private bool TryGetActor(out Guid tenantId, out Guid actorId)
    {
        tenantId = HttpContext.Items["DemandPlanning.TenantId"] is Guid value
            ? value : Guid.Empty;
        actorId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");
        return tenantId != Guid.Empty && Guid.TryParse(claim, out actorId) &&
            actorId != Guid.Empty;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateManualDraftRequest(Guid PlanningCycleId, string Reason,
    IReadOnlyList<ManualDraftSeriesInput> Series);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EditManualWeekRequest(DraftWeekValueKind ValueKind,
    decimal? Quantity, string Reason, int ExpectedContentVersion);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DraftTransitionRequest(int ExpectedContentVersion,
    int ExpectedStateVersion, string? Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExcludeManualDraftSeriesRequest(string Reason,
    int ExpectedContentVersion, int ExpectedStateVersion);
