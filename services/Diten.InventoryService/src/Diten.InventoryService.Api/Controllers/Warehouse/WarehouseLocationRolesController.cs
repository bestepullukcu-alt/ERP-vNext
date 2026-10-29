using Diten.InventoryService.Api.Warehouse;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Application.Features.WarehouseLocationRoles;
using Diten.InventoryService.Application.Features.WarehouseLocationRoles.Commands;
using Diten.InventoryService.Application.Features.WarehouseLocationRoles.Queries;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers.Warehouse;

/// <summary>
/// WMS-BUNDLE v1 (draft §4) — MOD-0178 location roles under <c>/api/warehouse/location-roles</c> (DL-1). Every action carries its
/// <c>warehouse.location-roles.*</c> key; the scope is the token's tenant + the selected company (another company's or tenant's record is
/// 404 — pack §8.1, F-11; no CROSS_LE_FORBIDDEN for a record id). No business rule here: the body is parsed, the key / If-Match headers
/// read, and the handler decides. A deleted record answers its last state on delete (and on a replay); the ETag is its version.
/// </summary>
[Route("api/warehouse/location-roles")]
[Authorize]
public sealed class WarehouseLocationRolesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public WarehouseLocationRolesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(WarehousePermissions.LocationRolesRead)]
    public async Task<IActionResult> List([FromQuery] string? warehouseId, [FromQuery] string? role, [FromQuery] string? page, [FromQuery] string? pageSize,
        CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWarehouseLocationRoleListQuery(warehouseId, role, page, pageSize), ct));

    [HttpGet("{roleId}")]
    [HasPermission(WarehousePermissions.LocationRolesRead)]
    public async Task<IActionResult> Get(string roleId, CancellationToken ct)
        => WarehouseSupport.TryParseId(roleId, out var id)
            ? WithETag(await _mediator.Send(new GetWarehouseLocationRoleByIdQuery(id), ct))
            : NotFound();

    [HttpPost]
    [HasPermission(WarehousePermissions.LocationRolesManage)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.CreateRoleMembers, b => new
        {
            Role = new CreateLocationRoleInput(b.Uuid("locationId"), b.Has("roles") ? b.Roles("roles") : [],
                b.Has("acceptedStorageConditions") ? b.Strings("acceptedStorageConditions") : [], b.String("note")),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return WithETag(await _mediator.Send(new CreateWarehouseLocationRoleCommand(body.Role, key), ct));
    }

    [HttpPut("{roleId}")]
    [HasPermission(WarehousePermissions.LocationRolesManage)]
    public async Task<IActionResult> Update(string roleId, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(roleId, out var id)) return NotFound();
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.UpdateRoleMembers, b => new
        {
            Change = new UpdateLocationRoleInput(b.Has("roles") ? b.Roles("roles") : null,
                b.Has("acceptedStorageConditions") ? b.Strings("acceptedStorageConditions") : null, b.Has("note"), b.String("note")),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new UpdateWarehouseLocationRoleCommand(id, body.Change, key, version), ct));
    }

    [HttpDelete("{roleId}")]
    [HasPermission(WarehousePermissions.LocationRolesManage)]
    public async Task<IActionResult> Delete(string roleId, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(roleId, out var id)) return NotFound();
        if (WarehouseSupport.HeaderKeyError(Request.Headers[InventoryContract.IdempotencyKeyHeader].ToString(), out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new DeleteWarehouseLocationRoleCommand(id, key, version), ct));
    }

    [HttpPost("bulk-delete")]
    [HasPermission(WarehousePermissions.LocationRolesManage)]
    public async Task<IActionResult> BulkDelete(CancellationToken ct)
    {
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.BulkDeleteMembers, b => new
        {
            Items = b.BulkItems("items"),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return CreateActionResultInstance(await _mediator.Send(new BulkDeleteWarehouseLocationRoleCommand(body.Items, key), ct));
    }

    // ── plumbing ──────────────────────────────────────────────────────────────────────────────────────────

    private IActionResult WithETag(Response<WarehouseLocationRoleDto> response)
    {
        if (response.IsSuccessful && response.Data is not null)
        {
            Response.Headers.ETag = $"W/\"{response.Data.Version}\"";
        }

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

    private new IActionResult NotFound() => ContractFailure(StatusCodes.Status404NotFound, WarehouseErrorCodes.LocationRoleNotFound, "Location role not found.");
}
