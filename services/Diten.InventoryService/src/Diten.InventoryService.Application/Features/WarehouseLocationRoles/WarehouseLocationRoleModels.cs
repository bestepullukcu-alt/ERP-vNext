using Diten.InventoryService.Domain.Warehouse.Enums;

namespace Diten.InventoryService.Application.Features.WarehouseLocationRoles;

// WMS-BUNDLE draft §3.5 / §3.6 / §3.8 wire shapes of the location roles (unwrapped success bodies; an empty optional member is omitted,
// never written as null — WMS draft §1 rule 4, the service's JSON options).

/// <summary>
/// §3.6 <c>effectiveStorageConditions</c>: the accepted set in force on the node — its own tags, or the nearest tagged ancestor's
/// (<c>inheritedFromLocationId</c> / <c>Code</c> = that ancestor, the "üst düğümden" badge). Absent when nothing on the path is tagged.
/// </summary>
public sealed record EffectiveStorageConditionsDto(
    IReadOnlyList<string> Codes,
    string? InheritedFromLocationId = null,
    string? InheritedFromLocationCode = null);

/// <summary>§3.5 <c>WarehouseLocationRole</c> (+ §3.6 <c>effectiveStorageConditions</c>).</summary>
public sealed record WarehouseLocationRoleDto(
    string RoleId,
    string LegalEntityId,
    string WarehouseId,
    string LocationId,
    string? LocationCode,
    string? LocationType,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> AcceptedStorageConditions,
    EffectiveStorageConditionsDto? EffectiveStorageConditions,
    string? Note,
    int Version,
    string ContractVersion = Warehouse.WarehouseContract.ContractVersion);

/// <summary>§3.8 list page.</summary>
public sealed record WarehouseLocationRolePageDto(
    IReadOnlyList<WarehouseLocationRoleDto> Items,
    long Total,
    int Page,
    int PageSize,
    string ContractVersion = Warehouse.WarehouseContract.ContractVersion);

/// <summary>One row of <c>bulkDeleteLocationRoles</c>: <c>Deleted</c> (+ the version the delete produced), <c>Conflict</c> or <c>NotFound</c>.</summary>
public sealed record BulkDeleteLocationRoleRowDto(string RoleId, string Result, int? Version = null);

public sealed record BulkDeleteLocationRolesResultDto(
    IReadOnlyList<BulkDeleteLocationRoleRowDto> Items,
    string ContractVersion = Warehouse.WarehouseContract.ContractVersion);

/// <summary>The body of a create (parsed by the API reader; every rule is the handler's).</summary>
public sealed record CreateLocationRoleInput(Guid LocationId, IReadOnlyList<WarehouseRole> Roles, IReadOnlyList<string> AcceptedStorageConditions, string? Note);

/// <summary>The body of an update: a member that was not sent is null and keeps its value; <see cref="NoteSent"/> tells an absent note from a cleared one.</summary>
public sealed record UpdateLocationRoleInput(IReadOnlyList<WarehouseRole>? Roles, IReadOnlyList<string>? AcceptedStorageConditions, bool NoteSent, string? Note);

public sealed record BulkDeleteLocationRoleItem(Guid RoleId, int Version);
