namespace Diten.Web.Models.Inventory;

/// <summary>MOD-0173-FU02 permission keys the location screens gate on (pack §14) — defined by InventoryService.</summary>
public static class LocationPermissionKeys
{
    public const string Read = "inventory.locations.read";
    public const string Manage = "inventory.locations.manage";

    /// <summary>"View in stock list" is the target page's own gate (FU01 S-1).</summary>
    public const string StockRead = InventoryPermissionKeys.Read;
}

/// <summary>
/// LOCATION contract error code → resx key (FU02 pack §12/§13: every failure is a localized sentence that says what to
/// do; the code is never the message and "403"/"Forbidden" never reach the reader — UAS-001 §5). The keys live in the
/// <c>LocationsIndex</c> family (the one form, dialog and proxy vocabulary of L-1 and L-2) in all seven languages; a
/// test holds that. Kept apart from <see cref="InventoryErrorMessages"/>, whose keys the FU01 families must carry.
/// </summary>
public static class LocationErrorMessages
{
    public static readonly IReadOnlyDictionary<string, string> ByCode = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // FU02 pack §12 / §13, LOCATION v1.1 x-error-codes
        ["LOCATION_CODE_TAKEN"] = "ErrCodeTaken",
        ["LOCATION_LEVEL_INVALID"] = "ErrLevelInvalid",
        ["LOCATION_PARENT_INACTIVE"] = "ErrParentInactive",
        ["LOCATION_MOVE_INTO_SUBTREE"] = "ErrMoveIntoSubtree",
        ["LOCATION_HAS_STOCK"] = "ErrHasStock",
        ["LOCATION_HAS_ACTIVE_CHILDREN"] = "ErrHasActiveChildren",
        ["LOCATION_ARCHIVED"] = "ErrArchived",
        ["LOCATION_CONFLICT"] = "ErrLocationConflict",
        ["LOCATION_BULK_LIMIT"] = "ErrBulkLimit",
        ["UNKNOWN_LOCATION"] = "ErrUnknownLocation",
        ["ORG_UNIT_INVALID"] = "ErrOrgUnitInvalid",
        ["ORG_UNIT_DIRECTORY_UNAVAILABLE"] = "ErrOrgUnitDirectoryUnavailable",
        ["IDEMPOTENCY_KEY_REUSED"] = "ErrKeyReused",
        ["INVALID_REQUEST"] = "ErrInvalidRequest",
        ["CROSS_LE_FORBIDDEN"] = "ErrCrossLegalEntity",
        // W-0173-02 company selector
        ["LEGAL_ENTITY_MASTER_UNAVAILABLE"] = "ErrCompanyCheckUnavailable",
        [InventoryGatewayClient.CompanyNotSelected] = "ErrSelectCompany",
        // the MVC adapter's own refusal (never produced by the shipped JS)
        [LocationListAdapter.InvalidQuery] = "ErrInvalidRequest"
    };

    public const string NoPermission = "ErrNoPermission";
    public const string SessionExpired = "ErrSessionExpired";
    public const string ServiceUnavailable = "ErrServiceUnavailable";
    public const string Conflict = "ErrConflict";
    public const string Generic = "ErrGeneric";

    /// <summary>Every key this class can return — the resx parity test asserts each is in all seven files.</summary>
    public static IEnumerable<string> AllKeys =>
        ByCode.Values.Concat([NoPermission, SessionExpired, ServiceUnavailable, Conflict, Generic]).Distinct(StringComparer.Ordinal);

    /// <summary>The key for an answer: the contract code first, then the HTTP status.</summary>
    public static string KeyFor(string? code, int status)
    {
        if (code is not null && ByCode.TryGetValue(code, out var key)) return key;
        return status switch
        {
            401 => SessionExpired,
            403 => NoPermission,
            409 => Conflict,
            0 or 502 or 503 or 504 => ServiceUnavailable,
            _ => Generic
        };
    }
}
