using Diten.InventoryService.Api.Warehouse;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.PickTasks.Commands;
using Diten.InventoryService.Application.Features.PickTasks.Queries;
using Diten.InventoryService.Application.Features.PutawayTasks;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers.Warehouse;

/// <summary>
/// WMS-BUNDLE v1.1.0 (draft §11) — MOD-0178 S2 pick tasks under <c>/api/warehouse/pick-tasks</c> (DL-5). Every action carries its
/// <c>warehouse.pick-tasks.*</c> key; the scope is the token's tenant + the selected company (another company's or tenant's task is 404). "Supervisor"
/// is read from the token's <c>warehouse.pick-tasks.update</c> claim, never from the body. The confirmation body takes NO target and NO source
/// (400 — pack §8.19). No answer carries the ship-to (Q-10a). No business rule here.
/// </summary>
[Route("api/warehouse/pick-tasks")]
[Authorize]
public sealed class PickTasksController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PickTasksController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(WarehousePermissions.PickTasksRead)]
    public async Task<IActionResult> List([FromQuery] string[]? status, [FromQuery] string? warehouseId, [FromQuery] string? outboundId, [FromQuery] string? assigneeUserId,
        [FromQuery] string? q, [FromQuery] string? orderBy, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetPickTaskListQuery(status ?? [], warehouseId, outboundId, assigneeUserId, q, orderBy, page, pageSize), ct));

    [HttpGet("{taskId}")]
    [HasPermission(WarehousePermissions.PickTasksRead)]
    public async Task<IActionResult> Get(string taskId, CancellationToken ct)
        => WarehouseSupport.TryParseId(taskId, out var id) ? WithETag(await _mediator.Send(new GetPickTaskByIdQuery(id), ct)) : NotFound();

    [HttpGet("{taskId}/events")]
    [HasPermission(WarehousePermissions.PickTasksRead)]
    public async Task<IActionResult> Events(string taskId, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => WarehouseSupport.TryParseId(taskId, out var id)
            ? CreateActionResultInstance(await _mediator.Send(new GetPickTaskEventListQuery(id, page, pageSize), ct))
            : NotFound();

    [HttpPost("bulk-assign")]
    [HasPermission(WarehousePermissions.PickTasksUpdate)]
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
        return CreateActionResultInstance(await _mediator.Send(new BulkAssignPickTaskCommand(body.Assignee, body.Items, key), ct));
    }

    [HttpPost("{taskId}/claim")]
    [HasPermission(WarehousePermissions.PickTasksConfirm)]
    public async Task<IActionResult> Claim(string taskId, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(taskId, out var id)) return NotFound();
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.KeyMembers, b => new { Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new ClaimPickTaskCommand(id, key, version), ct));
    }

    [HttpPost("{taskId}/confirmations")]
    [HasPermission(WarehousePermissions.PickTasksConfirm)]
    public async Task<IActionResult> Confirm(string taskId, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(taskId, out var id)) return NotFound();
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.ConfirmPickMembers, b => new
        {
            Confirmation = new ConfirmPickTaskInput(b.String("quantity"), b.EnumOr("channel", ConfirmationChannel.Ui), b.String("scanRef")),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new ConfirmPickTaskCommand(id, body.Confirmation, key, version, Supervisor()), ct));
    }

    [HttpPost("{taskId}/confirmations/{seq:int}/retry")]
    [HasPermission(WarehousePermissions.PickTasksConfirm)]
    public async Task<IActionResult> Retry(string taskId, int seq, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(taskId, out var id)) return NotFound();
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.KeyMembers, b => new { Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return WithETag(await _mediator.Send(new RetryPickConfirmationCommand(id, seq, key, Supervisor()), ct));
    }

    [HttpPost("{taskId}/confirmations/{seq:int}/returned")]
    [HasPermission(WarehousePermissions.PickTasksConfirm)]
    public Task<IActionResult> MarkReturned(string taskId, int seq, CancellationToken ct)
        => Reasoned(taskId, (id, reason, key, version) => new MarkPickConfirmationReturnedCommand(id, seq, reason, key, version, Supervisor()), ct);

    [HttpPost("{taskId}/confirmations/{seq:int}/resolve-integrity")]
    [HasPermission(WarehousePermissions.PickTasksResolve)]
    public Task<IActionResult> ResolveIntegrity(string taskId, int seq, CancellationToken ct)
        => Reasoned(taskId, (id, reason, key, version) => new ResolvePickIntegrityCommand(id, seq, reason, key, version), ct);

    [HttpPost("{taskId}/close-short")]
    [HasPermission(WarehousePermissions.PickTasksConfirm)]
    public Task<IActionResult> CloseShort(string taskId, CancellationToken ct)
        => Reasoned(taskId, (id, reason, key, version) => new ClosePickTaskShortCommand(id, reason, key, version, Supervisor()), ct);

    // ── plumbing ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>§8.13: a supervisor holds <c>warehouse.pick-tasks.update</c> — read from the signed token only.</summary>
    private bool Supervisor() => User.Claims.Any(c => c.Type == "permission" && string.Equals(c.Value, WarehousePermissions.PickTasksUpdate, StringComparison.OrdinalIgnoreCase));

    private async Task<IActionResult> Reasoned(string taskId, Func<Guid, string?, string, int, IRequest<Response<WarehouseTaskDto>>> command, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(taskId, out var id)) return NotFound();
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.ReasonMembers, b => new { Reason = b.String("reason"), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(command(id, body.Reason, key, version), ct));
    }

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

    private new IActionResult NotFound() => ContractFailure(StatusCodes.Status404NotFound, WarehouseErrorCodes.TaskNotFound, "Pick task not found.");
}
