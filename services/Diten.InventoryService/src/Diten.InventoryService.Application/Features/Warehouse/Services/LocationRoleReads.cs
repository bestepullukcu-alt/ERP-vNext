using Diten.InventoryService.Application.Features.WarehouseLocationRoles;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;

namespace Diten.InventoryService.Application.Features.Warehouse.Services;

/// <summary>The roles and the storage-condition set in force on one LOCATION node (pack §4.4 F-13, §8.12).</summary>
public sealed record LocationRoleView(Location Location, IReadOnlySet<Domain.Warehouse.Enums.WarehouseRole> EffectiveRoles, EffectiveStorageConditions? EffectiveConditions);

/// <summary>
/// The reads of MOD-0178's location roles over the in-process LOCATION store (MOD-0173-FU02 — <c>getLocation</c> / <c>getLocationPath</c> are
/// the company-bound <see cref="ILocationRepository"/>; the path is the node's materialized <c>AncestorIds</c>). It answers the wire shape of a
/// role record with its effective conditions, and — for any node, with or without its own record — the roles and conditions inherited down
/// to it (DL-2's source / target rules and storage-condition fit read <see cref="ViewAsync"/>).
/// </summary>
public sealed class LocationRoleReads
{
    private readonly ILocationRepository _locations;
    private readonly IWarehouseLocationRoleRepository _roles;

    public LocationRoleReads(ILocationRepository locations, IWarehouseLocationRoleRepository roles)
    {
        _locations = locations;
        _roles = roles;
    }

    /// <summary>The node, then its parent … up to the root — every one of the caller's company (a missing ancestor is skipped).</summary>
    public async Task<IReadOnlyList<Location>> PathAsync(Location node, CancellationToken ct)
    {
        var ancestors = await _locations.FindManyAsync(node.AncestorIds, ct);
        var byId = ancestors.ToDictionary(a => a.Id);
        var path = new List<Location> { node };
        for (var i = node.AncestorIds.Count - 1; i >= 0; i--)
        {
            if (byId.TryGetValue(node.AncestorIds[i], out var ancestor)) path.Add(ancestor);
        }

        return path;
    }

    /// <summary>The roles and conditions in force on <paramref name="locationId"/>; null = no such node in the caller's company.</summary>
    public async Task<LocationRoleView?> ViewAsync(Guid locationId, CancellationToken ct)
    {
        var node = await _locations.FindAsync(locationId, ct);
        if (node is null) return null;
        var path = await PathAsync(node, ct);
        var records = await _roles.FindByLocationsAsync(path.Select(p => p.Id).ToList(), ct);
        var nodes = RoleNodes(path, records);
        return new LocationRoleView(node, LocationRoleRules.EffectiveRoles(nodes), LocationRoleRules.EffectiveConditions(nodes));
    }

    public async Task<WarehouseLocationRoleDto> DtoAsync(WarehouseLocationRole role, CancellationToken ct) => (await DtosAsync([role], ct))[0];

    /// <summary>The wire shape of each record (§3.5 + §3.6) — one LOCATION read and one role read for the whole page.</summary>
    public async Task<IReadOnlyList<WarehouseLocationRoleDto>> DtosAsync(IReadOnlyList<WarehouseLocationRole> roles, CancellationToken ct)
    {
        if (roles.Count == 0) return [];
        var nodes = await _locations.FindManyAsync(roles.Select(r => r.LocationId).Distinct().ToList(), ct);
        var ancestorIds = nodes.SelectMany(n => n.AncestorIds).Distinct().ToList();
        var ancestors = await _locations.FindManyAsync(ancestorIds, ct);
        var locations = nodes.Concat(ancestors).GroupBy(l => l.Id).ToDictionary(g => g.Key, g => g.First());
        var records = (await _roles.FindByLocationsAsync(locations.Keys.ToList(), ct)).ToDictionary(r => r.LocationId);

        return roles.Select(role =>
        {
            locations.TryGetValue(role.LocationId, out var node);
            EffectiveStorageConditions? effective = null;
            Guid? warehouseId = null;
            if (node is not null)
            {
                var path = new List<Location> { node };
                for (var i = node.AncestorIds.Count - 1; i >= 0; i--)
                {
                    if (locations.TryGetValue(node.AncestorIds[i], out var ancestor)) path.Add(ancestor);
                }

                // the record itself stands for its node (a replayed / deleted record included); the ancestors' live records follow
                var own = new Dictionary<Guid, WarehouseLocationRole>(records) { [role.LocationId] = role };
                effective = LocationRoleRules.EffectiveConditions(RoleNodes(path, own.Values.ToList()));
                warehouseId = path.FirstOrDefault(p => p.Type == Domain.Enums.LocationType.Warehouse)?.Id; // the CURRENT path (a K-07 move)
            }

            return Map(role, node, warehouseId ?? role.WarehouseId, effective,
                effective is { Inherited: true } && locations.TryGetValue(effective.SourceLocationId, out var source) ? source.Code : null);
        }).ToList();
    }

    private static List<LocationRoleNode> RoleNodes(IReadOnlyList<Location> path, IReadOnlyList<WarehouseLocationRole> records)
    {
        var byLocation = records.GroupBy(r => r.LocationId).ToDictionary(g => g.Key, g => g.First());
        return path.Select(p => byLocation.TryGetValue(p.Id, out var r)
                ? new LocationRoleNode(p.Id, r.Roles, r.AcceptedStorageConditions)
                : new LocationRoleNode(p.Id, [], []))
            .ToList();
    }

    private static WarehouseLocationRoleDto Map(WarehouseLocationRole role, Location? node, Guid warehouseId, EffectiveStorageConditions? effective,
        string? sourceCode)
        => new(
            role.Id.ToString("D"),
            (role.LegalEntityId ?? Guid.Empty).ToString("D"),
            warehouseId.ToString("D"),
            role.LocationId.ToString("D"),
            node?.Code,
            node is null ? null : node.Type.ToString(),
            role.Roles.Select(r => r.ToString()).ToList(),
            [.. role.AcceptedStorageConditions],
            effective is null
                ? null
                : new EffectiveStorageConditionsDto([.. effective.Codes],
                    effective.Inherited ? effective.SourceLocationId.ToString("D") : null,
                    effective.Inherited ? sourceCode : null),
            role.Note,
            role.Version);
}
