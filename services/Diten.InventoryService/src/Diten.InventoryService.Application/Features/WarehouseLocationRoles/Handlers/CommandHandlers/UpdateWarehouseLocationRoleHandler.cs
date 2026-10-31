using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Application.Features.WarehouseLocationRoles.Commands;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.WarehouseLocationRoles.Handlers.CommandHandlers;

/// <summary>
/// <c>updateLocationRole</c> (pack §4.4, §8.1, §12). The caller's key event of this role and operation is looked up BEFORE If-Match and every
/// business rule (a network repeat of a processed request is a 200 replay even though its If-Match is now stale — independent review 10 F-1);
/// then the live record (404), the version (409 <c>WMS_CONFLICT</c>), the shape and conflict rules of the resulting record. The record
/// (version + 1, compare-and-set on the read version) and its <c>Updated</c> key event (before / after, key, operation, fingerprint) are
/// written in one transaction; a lost compare-and-set or a duplicate key event re-reads the key before answering 409.
/// </summary>
public sealed class UpdateWarehouseLocationRoleHandler : IRequestHandler<UpdateWarehouseLocationRoleCommand, Response<WarehouseLocationRoleDto>>
{
    private readonly IWarehouseLocationRoleRepository _repository;
    private readonly ILocationRepository _locations;
    private readonly LocationRoleChecks _checks;
    private readonly LocationRoleReads _reads;
    private readonly WarehouseSupport _support;

    public UpdateWarehouseLocationRoleHandler(IWarehouseLocationRoleRepository repository, ILocationRepository locations, LocationRoleChecks checks,
        LocationRoleReads reads, WarehouseSupport support)
    {
        _repository = repository;
        _locations = locations;
        _checks = checks;
        _reads = reads;
        _support = support;
    }

    public async Task<Response<WarehouseLocationRoleDto>> Handle(UpdateWarehouseLocationRoleCommand request, CancellationToken ct)
    {
        var change = request.Change;
        var note = WarehouseSupport.Trimmed(change.Note);
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpUpdateLocationRole), ("roleId", request.RoleId.ToString("D")),
            ("roles", change.Roles is null ? null : WarehouseSupport.List(change.Roles.Select(r => r.ToString()).OrderBy(r => r, StringComparer.Ordinal))),
            ("acceptedStorageConditions", change.AcceptedStorageConditions is null ? null : WarehouseSupport.List(change.AcceptedStorageConditions.OrderBy(c => c, StringComparer.Ordinal))),
            ("noteSent", change.NoteSent ? "1" : "0"), ("note", note));

        if (await ReplayAsync(request.RoleId, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var role = await _repository.FindAsync(request.RoleId, ct);
        if (role is null) return WarehouseSupport.RoleNotFound<WarehouseLocationRoleDto>();
        if (role.Version != request.ExpectedVersion) return WarehouseSupport.Conflict<WarehouseLocationRoleDto>();

        var roles = change.Roles ?? role.Roles;
        var conditions = change.AcceptedStorageConditions ?? role.AcceptedStorageConditions;
        var newNote = change.NoteSent ? note : role.Note;
        if (LocationRoleChecks.ShapeRefusal<WarehouseLocationRoleDto>(roles, conditions, newNote) is { } shape) return shape;

        var canonicalRoles = LocationRoleRules.Canonical(roles);
        if (role.Roles.Contains(WarehouseRole.Receiving) && !canonicalRoles.Contains(WarehouseRole.Receiving)
            && await _checks.OpenTasksAsync<WarehouseLocationRoleDto>(role.LocationId, ct) is { } busy)
        {
            return busy;
        }

        var node = await _locations.FindAsync(role.LocationId, ct);
        if (node is not null && await _checks.InheritedConflictAsync<WarehouseLocationRoleDto>(node, canonicalRoles, ct) is { } conflict)
        {
            return conflict;
        }

        var now = _support.Now();
        var before = WarehouseLocationRoleSnapshot.Of(role);
        role.Roles = canonicalRoles;
        role.AcceptedStorageConditions = LocationRoleRules.Canonical(conditions);
        role.Note = newNote;
        role.Version = request.ExpectedVersion + 1;
        role.UpdatedAt = now;
        role.UpdatedBy = _support.UserId;
        var updated = new WarehouseLocationRoleEvent
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            RoleId = role.Id,
            Seq = role.Version,
            Kind = WarehouseLocationRoleEventKind.Updated,
            ActorUserId = _support.UserId,
            ActorName = _support.ActorName,
            AtUtc = now,
            Before = before,
            After = WarehouseLocationRoleSnapshot.Of(role),
            CorrelationId = _support.CorrelationId,
            IdempotencyKey = request.IdempotencyKey,
            RequestOp = WarehouseContract.OpUpdateLocationRole,
            RequestFingerprint = fingerprint
        };

        WarehouseWriteOutcome outcome;
        try
        {
            outcome = await _repository.InTransactionAsync(async (session, tx) =>
            {
                if (!await session.ReplaceRoleAsync(role, request.ExpectedVersion, tx)) return WarehouseWriteOutcome.None;
                await session.InsertEventAsync(updated, tx);
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

    private async Task<Response<WarehouseLocationRoleDto>?> ReplayAsync(Guid roleId, string key, string fingerprint, CancellationToken ct)
    {
        var earlier = await _repository.FindKeyEventAsync(roleId, _support.UserId, WarehouseContract.OpUpdateLocationRole, key, ct);
        if (earlier is null) return null;
        if (earlier.RequestFingerprint != fingerprint) return WarehouseSupport.KeyReused<WarehouseLocationRoleDto>();
        var current = await _repository.FindIncludingDeletedAsync(roleId, ct);
        return current is null ? WarehouseSupport.RoleNotFound<WarehouseLocationRoleDto>() : Response<WarehouseLocationRoleDto>.Success(await _reads.DtoAsync(current, ct));
    }
}
