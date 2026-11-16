using Diten.InventoryService.Api.Warehouse;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.PutawayTasks;
using Diten.InventoryService.Application.Features.PutawayTasks.Commands;
using Diten.InventoryService.Application.Features.PutawayTasks.Queries;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers.Warehouse;

/// <summary>
/// WMS-BUNDLE v1 (draft §4) — MOD-0178 putaway tasks and their candidates under <c>/api/warehouse</c> (DL-2). Every action carries its
/// <c>warehouse.putaway-tasks.*</c> key; the scope is the token's tenant + the selected company (another company's or tenant's task is 404 —
/// pack §8.1, F-11). "Supervisor" (the assignee rule's other half, §8.13) is read from the token's <c>warehouse.putaway-tasks.update</c> claim,
/// never from the body. No business rule here: the body is parsed, the key / If-Match headers read, and the handler decides.
/// </summary>
[Route("api/warehouse")]
[Authorize]
public sealed class PutawayTasksController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PutawayTasksController(IMediator mediator) => _mediator = mediator;

    // ── reads ────────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("putaway-candidates")]
    [HasPermission(WarehousePermissions.PutawayTasksRead)]
    public async Task<IActionResult> ListCandidates([FromQuery] string? warehouseId, [FromQuery] string? skuId, [FromQuery] string? lotId,
        [FromQuery] string? eligible, [FromQuery] string? q, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetPutawayCandidateListQuery(warehouseId, skuId, lotId, eligible, q, page, pageSize), ct));

    [HttpGet("putaway-tasks")]
    [HasPermission(WarehousePermissions.PutawayTasksRead)]
    public async Task<IActionResult> List([FromQuery] string[]? status, [FromQuery] string? warehouseId, [FromQuery] string? assigneeUserId,
        [FromQuery] string? priority, [FromQuery] string? q, [FromQuery] string? orderBy, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetPutawayTaskListQuery(status ?? [], warehouseId, assigneeUserId, priority, q, orderBy, page, pageSize), ct));

    [HttpGet("putaway-tasks/{taskId}")]
    [HasPermission(WarehousePermissions.PutawayTasksRead)]
    public async Task<IActionResult> Get(string taskId, CancellationToken ct)
        => WarehouseSupport.TryParseId(taskId, out var id) ? WithETag(await _mediator.Send(new GetPutawayTaskByIdQuery(id), ct)) : NotFound();

    [HttpGet("putaway-tasks/{taskId}/events")]
    [HasPermission(WarehousePermissions.PutawayTasksRead)]
    public async Task<IActionResult> Events(string taskId, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => WarehouseSupport.TryParseId(taskId, out var id)
            ? CreateActionResultInstance(await _mediator.Send(new GetPutawayTaskEventListQuery(id, page, pageSize), ct))
            : NotFound();

    // ── writes ───────────────────────────────────────────────────────────────────────────────────────────

    [HttpPost("putaway-tasks")]
    [HasPermission(WarehousePermissions.PutawayTasksCreate)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.CreateTaskMembers, b => new
        {
            Task = new CreatePutawayTaskInput(b.Source(), b.String("plannedQuantity"), b.OptionalUuid("toLocationId"),
                b.EnumOr("targetSource", WarehouseTargetSource.Manual), b.OptionalUuid("assigneeUserId"), b.EnumOr("priority", WarehouseTaskPriority.Normal),
                b.String("note")),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return WithETag(await _mediator.Send(new CreatePutawayTaskCommand(body.Task, key), ct));
    }

    [HttpPut("putaway-tasks/{taskId}")]
    [HasPermission(WarehousePermissions.PutawayTasksUpdate)]
    public async Task<IActionResult> Update(string taskId, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(taskId, out var id)) return NotFound();
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.UpdateTaskMembers, b => new
        {
            Change = new UpdatePutawayTaskInput(b.String("plannedQuantity"), b.Has("toLocationId"), b.OptionalUuid("toLocationId"),
                b.OptionalEnum<WarehouseTargetSource>("targetSource"), b.Has("assigneeUserId"), b.OptionalUuid("assigneeUserId"),
                b.OptionalEnum<WarehouseTaskPriority>("priority"), b.Has("note"), b.String("note")),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new UpdatePutawayTaskCommand(id, body.Change, key, version), ct));
    }

    [HttpPost("putaway-tasks/bulk-assign")]
    [HasPermission(WarehousePermissions.PutawayTasksUpdate)]
    public async Task<IActionResult> BulkAssign(CancellationToken ct)
    {
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.BulkAssignMembers, b => new
        {
            Assignee = b.Uuid("assigneeUserId"),
            Items = b.AssignItems("items"),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return CreateActionResultInstance(await _mediator.Send(new BulkAssignPutawayTaskCommand(body.Assignee, body.Items, key), ct));
    }

    [HttpPost("putaway-tasks/{taskId}/claim")]
    [HasPermission(WarehousePermissions.PutawayTasksConfirm)]
    public Task<IActionResult> Claim(string taskId, CancellationToken ct)
        => KeyOnly(taskId, requireIfMatch: true, (id, key, version) => new ClaimPutawayTaskCommand(id, key, version!.Value), ct);

    [HttpPost("putaway-tasks/{taskId}/confirmations")]
    [HasPermission(WarehousePermissions.PutawayTasksConfirm)]
    public async Task<IActionResult> Confirm(string taskId, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(taskId, out var id)) return NotFound();
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.ConfirmMembers, b => new
        {
            Confirmation = new ConfirmPutawayTaskInput(b.String("quantity"), b.Uuid("toLocationId"), b.String("deviationReason"),
                b.EnumOr("channel", ConfirmationChannel.Ui), b.String("scanRef")),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new ConfirmPutawayTaskCommand(id, body.Confirmation, key, version, Supervisor()), ct));
    }

    [HttpPost("putaway-tasks/{taskId}/confirmations/{seq:int}/retry")]
    [HasPermission(WarehousePermissions.PutawayTasksConfirm)]
    public Task<IActionResult> Retry(string taskId, int seq, CancellationToken ct)
        => KeyOnly(taskId, requireIfMatch: false, (id, key, _) => new RetryPutawayConfirmationCommand(id, seq, key, Supervisor()), ct);

    [HttpPost("putaway-tasks/{taskId}/confirmations/{seq:int}/returned")]
    [HasPermission(WarehousePermissions.PutawayTasksConfirm)]
    public Task<IActionResult> MarkReturned(string taskId, int seq, CancellationToken ct)
        => Reasoned(taskId, (id, reason, key, version) => new MarkPutawayConfirmationReturnedCommand(id, seq, reason, key, version, Supervisor()), ct);

    [HttpPost("putaway-tasks/{taskId}/confirmations/{seq:int}/resolve-integrity")]
    [HasPermission(WarehousePermissions.PutawayTasksResolve)]
    public Task<IActionResult> ResolveIntegrity(string taskId, int seq, CancellationToken ct)
        => Reasoned(taskId, (id, reason, key, version) => new ResolvePutawayIntegrityCommand(id, seq, reason, key, version), ct);

    [HttpPost("putaway-tasks/{taskId}/close-short")]
    [HasPermission(WarehousePermissions.PutawayTasksConfirm)]
    public Task<IActionResult> CloseShort(string taskId, CancellationToken ct)
        => Reasoned(taskId, (id, reason, key, version) => new ClosePutawayTaskShortCommand(id, reason, key, version, Supervisor()), ct);

    [HttpPost("putaway-tasks/{taskId}/cancel")]
    [HasPermission(WarehousePermissions.PutawayTasksCancel)]
    public Task<IActionResult> Cancel(string taskId, CancellationToken ct)
        => Reasoned(taskId, (id, reason, key, version) => new CancelPutawayTaskCommand(id, reason, key, version), ct);

    // ── plumbing ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>§8.13: a supervisor holds <c>warehouse.putaway-tasks.update</c> — read from the signed token only.</summary>
    private bool Supervisor() => User.Claims.Any(c => c.Type == "permission" && string.Equals(c.Value, WarehousePermissions.PutawayTasksUpdate, StringComparison.OrdinalIgnoreCase));

    private async Task<IActionResult> KeyOnly(string taskId, bool requireIfMatch, Func<Guid, string, int?, IRequest<Response<WarehouseTaskDto>>> command, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(taskId, out var id)) return NotFound();
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.KeyMembers, b => new KeyBody(b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        var version = IfMatch();
        if (requireIfMatch && version is null) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(command(id, key, version), ct));
    }

    private async Task<IActionResult> Reasoned(string taskId, Func<Guid, string?, string, int, IRequest<Response<WarehouseTaskDto>>> command, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(taskId, out var id)) return NotFound();
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.ReasonMembers,
            b => new ReasonBody(b.String("reason"), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(command(id, body.Reason, key, version), ct));
    }

    private sealed record KeyBody(string? Key);

    private sealed record ReasonBody(string? Reason, string? Key);

    private IActionResult WithETag(Response<WarehouseTaskDto> response)
    {
        if (response.IsSuccessful && response.Data is not null) Response.Headers.ETag = $"W/\"{response.Data.Version}\"";
        return CreateActionResultInstance(response);
    }

    private string? KeyError(string? body, out string key)
        => WarehouseSupport.KeyError(Request.Headers[InventoryContract.IdempotencyKeyHeader].ToString(), body, out key);

    private int? IfMatch()
    {
        var header = Request.Headers[InventoryContract.IfMatchHeader].ToString();
        return string.IsNullOrWhiteSpace(header) ? null : WarehouseSupport.ExpectedVersion(header);
    }

    private IActionResult Invalid(string? message)
        => ContractFailure(StatusCodes.Status400BadRequest, InventoryErrorCodes.InvalidRequest, message ?? "The request is malformed.");

    private new IActionResult NotFound() => ContractFailure(StatusCodes.Status404NotFound, WarehouseErrorCodes.TaskNotFound, "Putaway task not found.");
}
