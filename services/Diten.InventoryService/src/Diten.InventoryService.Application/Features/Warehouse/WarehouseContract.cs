namespace Diten.InventoryService.Application.Features.Warehouse;

/// <summary>
/// WMS-BUNDLE v1 (draft <c>docs/analysis/contracts/drafts/wms-bundle-v1-proposal.md</c>, owner MOD-0178) — the constants the DL-1 slice
/// serves: base path, idempotency-key bounds, paging and the WMS <c>operationId</c>s stored as <c>RequestOp</c> on the key events (pack §4.3).
/// </summary>
public static class WarehouseContract
{
    public const string ModuleId = "MOD-0178";

    public const string BasePath = "/api/warehouse";

    public const string ContractVersion = "v1";

    public const int MinIdempotencyKeyLength = 8;
    public const int MaxIdempotencyKeyLength = 100;

    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;

    /// <summary>The rows one bulk delete may carry (the list page size).</summary>
    public const int MaxBulkItems = 200;

    // WMS operationIds of the location-role writes (pack §4.3 "RequestOp", §4.6 key-event indexes)
    public const string OpCreateLocationRole = "createLocationRole";
    public const string OpUpdateLocationRole = "updateLocationRole";
    public const string OpDeleteLocationRole = "deleteLocationRole";
    public const string OpBulkDeleteLocationRoles = "bulkDeleteLocationRoles";

    // bulk-delete row results (WMS draft §4, rule 5)
    public const string RowDeleted = "Deleted";
    public const string RowConflict = "Conflict";
    public const string RowNotFound = "NotFound";
}
