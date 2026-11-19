using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Application.Features.WarehouseLocationRoles.Commands;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.WarehouseLocationRoles.Handlers.CommandHandlers;

/// <summary>
/// <c>createLocationRole</c> (pack §4.4, §8.1, §12). Order: the caller's create key first (same fingerprint → 200 replay of the record's current
/// state, other → 400 <c>IDEMPOTENCY_KEY_REUSED</c>), then the shape rules, the LOCATION node (active, of the company, StorageLocation | Zone |
/// Bin, under a Warehouse), one record per node, the inherited-role conflict. The record (version 1) and its <c>Created</c> history row are
/// written in ONE transaction that enlists the central audit event. A unique-index refusal — whichever index Mongo names, the create key's
/// or the node's — re-reads the key first (a concurrent twin of this request committed → replay); only without it is a node duplicate
/// 409 <c>WMS_LOCATION_ROLE_EXISTS</c> (independent review 11 F-3).
/// </summary>
public sealed class CreateWarehouseLocationRoleHandler : IRequestHandler<CreateWarehouseLocationRoleCommand, Response<WarehouseLocationRoleDto>>
{
    private readonly IWarehouseLocationRoleRepository _repository;
    private readonly ILocationRepository _locations;
    private readonly LocationRoleChecks _checks;
    private readonly LocationRoleReads _reads;
    private readonly WarehouseSupport _support;

    public CreateWarehouseLocationRoleHandler(IWarehouseLocationRoleRepository repository, ILocationRepository locations, LocationRoleChecks checks,
        LocationRoleReads reads, WarehouseSupport support)
    {
        _repository = repository;
        _locations = locations;
        _checks = checks;
        _reads = reads;
        _support = support;
    }

    public async Task<Response<WarehouseLocationRoleDto>> Handle(CreateWarehouseLocationRoleCommand request, CancellationToken ct)
    {
        var input = request.Role;
        var note = WarehouseSupport.Trimmed(input.Note);
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpCreateLocationRole), ("locationId", input.LocationId.ToString("D")),
            ("roles", WarehouseSupport.List(input.Roles.Select(r => r.ToString()).OrderBy(r => r, StringComparer.Ordinal))),
            ("acceptedStorageConditions", WarehouseSupport.List(input.AcceptedStorageConditions.OrderBy(c => c, StringComparer.Ordinal))),
            ("note", note));

        if (await ReplayAsync(request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        if (LocationRoleChecks.ShapeRefusal<WarehouseLocationRoleDto>(input.Roles, input.AcceptedStorageConditions, note) is { } shape) return shape;

        var node = await _locations.FindAsync(input.LocationId, ct);
        if (node is null || node.Status != LocationStatus.Active) return LocationRoleChecks.UnknownLocation<WarehouseLocationRoleDto>();
        if (!LocationRoleRules.IsRoleLocationType(node.Type))
        {
            return WarehouseSupport.Fail<WarehouseLocationRoleDto>(WarehouseErrorCodes.LocationTypeNotAllowed,
                "A location role is given to a storage location, zone or bin — not to a site or warehouse.", 422,
                new Dictionary<string, object?> { ["locationType"] = node.Type.ToString() });
        }

        var warehouse = await _checks.WarehouseOfAsync(node, ct);
        if (warehouse is null) return LocationRoleChecks.UnknownLocation<WarehouseLocationRoleDto>();

        if (await _repository.FindByLocationAsync(node.Id, ct) is { } existing) return Exists(existing.Id);

        var roles = LocationRoleRules.Canonical(input.Roles);
        if (await _checks.InheritedConflictAsync<WarehouseLocationRoleDto>(node, roles, ct) is { } conflict) return conflict;

        var now = _support.Now();
        var role = new WarehouseLocationRole
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Version = 1,
            WarehouseId = warehouse.Id,
            LocationId = node.Id,
            Roles = roles,
            AcceptedStorageConditions = LocationRoleRules.Canonical(input.AcceptedStorageConditions),
            Note = note,
            CreatedBy = _support.UserId,
            IdempotencyKey = request.IdempotencyKey,
            RequestFingerprint = fingerprint
        };
        var created = new WarehouseLocationRoleEvent
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            RoleId = role.Id,
            Seq = role.Version,
            Kind = WarehouseLocationRoleEventKind.Created,
            ActorUserId = _support.UserId,
            ActorName = _support.ActorName,
            AtUtc = now,
            After = WarehouseLocationRoleSnapshot.Of(role),
            CorrelationId = _support.CorrelationId
        };

        try
        {
            await _repository.InTransactionAsync(async (session, tx) =>
            {
                await session.InsertRoleAsync(role, tx);
                await session.InsertEventAsync(created, tx);
                return new WarehouseWriteOutcome(1, role.Id);
            }, ct);
        }
        catch (WarehouseLocationRoleDuplicateException duplicate)
        {
            if (await ReplayAsync(request.IdempotencyKey, fingerprint, ct) is { } twin) return twin;
            return duplicate.Index == WarehouseLocationRoleUniqueIndex.Location && await _repository.FindByLocationAsync(node.Id, ct) is { } other
                ? Exists(other.Id)
                : WarehouseSupport.Conflict<WarehouseLocationRoleDto>();
        }

        return Response<WarehouseLocationRoleDto>.Success(await _reads.DtoAsync(role, ct), 201);
    }

    /// <summary>null = the caller has no record under this key; otherwise the replay (200, current state) or 400.</summary>
    private async Task<Response<WarehouseLocationRoleDto>?> ReplayAsync(string key, string fingerprint, CancellationToken ct)
    {
        var earlier = await _repository.FindByCreateKeyAsync(_support.UserId, key, ct);
        if (earlier is null) return null;
        return earlier.RequestFingerprint == fingerprint
            ? Response<WarehouseLocationRoleDto>.Success(await _reads.DtoAsync(earlier, ct))
            : WarehouseSupport.KeyReused<WarehouseLocationRoleDto>();
    }

    private static Response<WarehouseLocationRoleDto> Exists(Guid roleId)
        => WarehouseSupport.Fail<WarehouseLocationRoleDto>(WarehouseErrorCodes.LocationRoleExists, "This location already has a role record.", 409,
            new Dictionary<string, object?> { ["roleId"] = roleId.ToString("D") });
}
