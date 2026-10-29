using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;

namespace Diten.InventoryService.Application.Features.Warehouse.Services;

/// <summary>
/// The rules a role record must satisfy before it is written — shared by create and update (pack §4.4, §12): the shape of the sets
/// (400), Receiving + Staging on one node (409 <c>WMS_LOCATION_ROLE_CONFLICT</c>) and the conflict with an ancestor or a node of the
/// subtree (F-13, 409 <c>WMS_LOCATION_ROLE_INHERITED_CONFLICT</c>). The open-task rule of removing Receiving / deleting a record
/// (409 <c>WMS_LOCATION_HAS_OPEN_TASKS</c>) needs tasks and comes with DL-2.
/// </summary>
public sealed class LocationRoleChecks
{
    private readonly ILocationRepository _locations;
    private readonly IWarehouseLocationRoleRepository _roles;
    private readonly LocationRoleReads _reads;

    public LocationRoleChecks(ILocationRepository locations, IWarehouseLocationRoleRepository roles, LocationRoleReads reads)
    {
        _locations = locations;
        _roles = roles;
        _reads = reads;
    }

    /// <summary>The request-shape rules of a role record; null = valid.</summary>
    public static Response<T>? ShapeRefusal<T>(IReadOnlyList<WarehouseRole> roles, IReadOnlyList<string> conditions, string? note)
    {
        if (LocationRoleRules.CheckRoles(roles) == RoleSetProblem.Duplicate) return WarehouseSupport.Invalid<T>("roles must not repeat a role.");
        var tags = LocationRoleRules.CheckConditionTags(conditions);
        if (tags != ConditionTagProblem.None)
        {
            return WarehouseSupport.Fail<T>(WarehouseErrorCodes.StorageConditionInvalid, tags switch
            {
                ConditionTagProblem.Unknown => "acceptedStorageConditions may only hold " + string.Join(", ", LocationRoleRules.KnownStorageConditions) + ".",
                ConditionTagProblem.Duplicate => "acceptedStorageConditions must not repeat a code.",
                _ => "acceptedStorageConditions may hold at most one temperature code (" + string.Join(", ", LocationRoleRules.TemperatureConditions) + ")."
            }, 400, new Dictionary<string, object?> { ["acceptedStorageConditions"] = conditions.ToArray() });
        }

        if (LocationRoleRules.IsEmpty(roles, conditions))
        {
            return WarehouseSupport.Fail<T>(WarehouseErrorCodes.StorageConditionInvalid,
                "A location role needs at least one role or one accepted storage condition.", 400);
        }

        if (note is { Length: > LocationRoleRules.MaxNoteLength })
        {
            return WarehouseSupport.Invalid<T>($"note must be at most {LocationRoleRules.MaxNoteLength} characters.");
        }

        return LocationRoleRules.CheckRoles(roles) == RoleSetProblem.ReceivingWithStaging
            ? WarehouseSupport.Fail<T>(WarehouseErrorCodes.LocationRoleConflict, "A location cannot be both Receiving and Staging.", 409)
            : null;
    }

    /// <summary>
    /// F-13: <paramref name="roles"/> on <paramref name="node"/> against the roles of its ancestors (inherited down to it) and of the nodes of
    /// its subtree (which would inherit them) — the subtree read from the CURRENT LOCATION paths; null = no conflict.
    /// </summary>
    public async Task<Response<T>?> InheritedConflictAsync<T>(Location node, IReadOnlyCollection<WarehouseRole> roles, CancellationToken ct)
    {
        if (roles.Count == 0) return null;

        var path = await _reads.PathAsync(node, ct);
        var ancestorIds = path.Skip(1).Select(p => p.Id).ToList();
        var ancestorRecords = ancestorIds.Count == 0 ? [] : await _roles.FindByLocationsAsync(ancestorIds, ct);

        var descendantRoles = new List<WarehouseRole>();
        foreach (var opposite in roles.Select(LocationRoleRules.Opposite).Distinct())
        {
            var holders = (await _roles.FindWithRoleAsync(opposite, ct)).Where(r => r.LocationId != node.Id).ToList();
            if (holders.Count == 0) continue;
            var places = await _locations.FindManyAsync(holders.Select(h => h.LocationId).ToList(), ct);
            if (places.Any(p => p.AncestorIds.Contains(node.Id))) descendantRoles.Add(opposite);
        }

        var conflict = LocationRoleRules.InheritedConflict(roles, ancestorRecords.SelectMany(r => r.Roles), descendantRoles);
        return conflict is { } role
            ? WarehouseSupport.Fail<T>(WarehouseErrorCodes.LocationRoleInheritedConflict,
                $"The location cannot take the {role} role: an ancestor or a location below it holds {LocationRoleRules.Opposite(role)}.", 409,
                new Dictionary<string, object?> { ["role"] = role.ToString(), ["conflictingRole"] = LocationRoleRules.Opposite(role).ToString() })
            : null;
    }

    /// <summary>The Warehouse on the node's LOCATION path; null = the node is not under a warehouse of the caller's company.</summary>
    public async Task<Location?> WarehouseOfAsync(Location node, CancellationToken ct)
        => (await _reads.PathAsync(node, ct)).FirstOrDefault(p => p.Type == Domain.Enums.LocationType.Warehouse);

    public static Response<T> UnknownLocation<T>()
        => WarehouseSupport.Fail<T>(InventoryErrorCodes.UnknownLocation, "The location does not exist in the selected company or is not active.", 422);
}
