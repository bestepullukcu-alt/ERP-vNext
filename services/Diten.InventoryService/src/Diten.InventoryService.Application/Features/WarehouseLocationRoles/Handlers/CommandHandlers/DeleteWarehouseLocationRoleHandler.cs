using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Application.Features.WarehouseLocationRoles.Commands;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.WarehouseLocationRoles.Handlers.CommandHandlers;

/// <summary>
/// <c>deleteLocationRole</c> (pack §4.4, §8.1) — soft delete only (<c>IsDeleted</c>, <c>DeletedAt</c>; multi-tenancy rule). The key event of this
/// role and operation first (replay = 200 + the record's current, deleted state), then the live record (404) and its version (409). The deletion
/// (version + 1) and its <c>Deleted</c> key event (before values) are one transaction. A node with open tasks below keeps its record
/// (409 <c>WMS_LOCATION_HAS_OPEN_TASKS</c>, DL-2 / AC-58).
/// </summary>
public sealed class DeleteWarehouseLocationRoleHandler : IRequestHandler<DeleteWarehouseLocationRoleCommand, Response<WarehouseLocationRoleDto>>
{
    private readonly IWarehouseLocationRoleRepository _repository;
    private readonly LocationRoleReads _reads;
    private readonly LocationRoleChecks _checks;
    private readonly WarehouseSupport _support;

    public DeleteWarehouseLocationRoleHandler(IWarehouseLocationRoleRepository repository, LocationRoleReads reads, LocationRoleChecks checks, WarehouseSupport support)
    {
        _repository = repository;
        _reads = reads;
        _checks = checks;
        _support = support;
    }

    public async Task<Response<WarehouseLocationRoleDto>> Handle(DeleteWarehouseLocationRoleCommand request, CancellationToken ct)
    {
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpDeleteLocationRole), ("roleId", request.RoleId.ToString("D")));
        if (await ReplayAsync(request.RoleId, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var role = await _repository.FindAsync(request.RoleId, ct);
        if (role is null) return WarehouseSupport.RoleNotFound<WarehouseLocationRoleDto>();
        if (role.Version != request.ExpectedVersion) return WarehouseSupport.Conflict<WarehouseLocationRoleDto>();
        if (await _checks.OpenTasksAsync<WarehouseLocationRoleDto>(role.LocationId, ct) is { } busy) return busy;

        var now = _support.Now();
        var before = WarehouseLocationRoleSnapshot.Of(role);
        role.IsDeleted = true;
        role.DeletedAt = now;
        role.UpdatedAt = now;
        role.UpdatedBy = _support.UserId;
        role.Version = request.ExpectedVersion + 1;
        var deleted = DeletedEvent(_support, role, before, now, request.IdempotencyKey, WarehouseContract.OpDeleteLocationRole, fingerprint);

        WarehouseWriteOutcome outcome;
        try
        {
            outcome = await _repository.InTransactionAsync(async (session, tx) =>
            {
                if (!await session.ReplaceRoleAsync(role, request.ExpectedVersion, tx)) return WarehouseWriteOutcome.None;
                await session.InsertEventAsync(deleted, tx);
                return new WarehouseWriteOutcome(1, role.Id);
            }, ct);
        }
        catch (WarehouseLocationRoleDuplicateException)
        {
            outcome = WarehouseWriteOutcome.None;
        }

        if (outcome.Affected == 0)
        {
            return await ReplayAsync(request.RoleId, request.IdempotencyKey, fingerprint, ct) ?? WarehouseSupport.Conflict<WarehouseLocationRoleDto>();
        }

        return Response<WarehouseLocationRoleDto>.Success(await _reads.DtoAsync(role, ct));
    }

    /// <summary>The <c>Deleted</c> history row of one record — the key event of a single or a bulk delete (pack §4.3).</summary>
    internal static WarehouseLocationRoleEvent DeletedEvent(WarehouseSupport support, WarehouseLocationRole role, WarehouseLocationRoleSnapshot before,
        DateTimeOffset now, string key, string op, string fingerprint) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = now,
        RoleId = role.Id,
        Seq = role.Version,
        Kind = WarehouseLocationRoleEventKind.Deleted,
        ActorUserId = support.UserId,
        ActorName = support.ActorName,
        AtUtc = now,
        Before = before,
        CorrelationId = support.CorrelationId,
        IdempotencyKey = key,
        RequestOp = op,
        RequestFingerprint = fingerprint
    };

    private async Task<Response<WarehouseLocationRoleDto>?> ReplayAsync(Guid roleId, string key, string fingerprint, CancellationToken ct)
    {
        var earlier = await _repository.FindKeyEventAsync(roleId, _support.UserId, WarehouseContract.OpDeleteLocationRole, key, ct);
        if (earlier is null) return null;
        if (earlier.RequestFingerprint != fingerprint) return WarehouseSupport.KeyReused<WarehouseLocationRoleDto>();
        var current = await _repository.FindIncludingDeletedAsync(roleId, ct);
        return current is null ? WarehouseSupport.RoleNotFound<WarehouseLocationRoleDto>() : Response<WarehouseLocationRoleDto>.Success(await _reads.DtoAsync(current, ct));
    }
}
