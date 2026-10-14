using Diten.InventoryService.Application.Common;

namespace Diten.InventoryService.Application.Features.Locations;

/// <summary>
/// Error codes of the LOCATION v1.1 owner operations (MOD-0173-FU02 §12 / §13 / §15). The shared codes
/// (<c>INVALID_REQUEST</c>, <c>IDEMPOTENCY_KEY_REUSED</c>, <c>CROSS_LE_FORBIDDEN</c>, <c>UNKNOWN_LOCATION</c>) keep the
/// MOD-0173 values.
/// </summary>
public static class LocationErrorCodes
{
    public const string UnknownLocation = InventoryErrorCodes.UnknownLocation;
    public const string InvalidRequest = InventoryErrorCodes.InvalidRequest;
    public const string IdempotencyKeyReused = InventoryErrorCodes.IdempotencyKeyReused;
    public const string CrossLeForbidden = InventoryErrorCodes.CrossLeForbidden;

    public const string CodeTaken = "LOCATION_CODE_TAKEN";
    public const string LevelInvalid = "LOCATION_LEVEL_INVALID";
    public const string ParentInactive = "LOCATION_PARENT_INACTIVE";
    public const string MoveIntoSubtree = "LOCATION_MOVE_INTO_SUBTREE";
    public const string HasStock = "LOCATION_HAS_STOCK";
    public const string HasActiveChildren = "LOCATION_HAS_ACTIVE_CHILDREN";
    public const string Archived = "LOCATION_ARCHIVED";
    public const string Conflict = "LOCATION_CONFLICT";
    public const string BulkLimit = "LOCATION_BULK_LIMIT";
    public const string OrgUnitInvalid = "ORG_UNIT_INVALID";
    public const string OrgUnitDirectoryUnavailable = "ORG_UNIT_DIRECTORY_UNAVAILABLE";
}
