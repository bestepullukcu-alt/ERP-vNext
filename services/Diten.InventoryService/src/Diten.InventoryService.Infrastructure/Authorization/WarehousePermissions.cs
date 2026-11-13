namespace Diten.InventoryService.Infrastructure.Authorization;

/// <summary>
/// MOD-0178 permission keys — namespace <c>warehouse.*</c>, owned by MOD-0178 (pack §14.1, decision Q-08; PKS-001 §4 one owner per namespace).
/// All Tenant scope. <see cref="Implemented"/> are the keys the controllers enforce so far — exactly what the manifest declares; the S2 pick,
/// cancel / release and package keys come with their slices (DL-5).
/// </summary>
public static class WarehousePermissions
{
    public const string PutawayTasksRead = "warehouse.putaway-tasks.read";

    public const string PutawayTasksCreate = "warehouse.putaway-tasks.create";

    public const string PutawayTasksUpdate = "warehouse.putaway-tasks.update";

    public const string PutawayTasksConfirm = "warehouse.putaway-tasks.confirm";

    public const string PutawayTasksCancel = "warehouse.putaway-tasks.cancel";

    public const string PutawayTasksResolve = "warehouse.putaway-tasks.resolve";

    public const string LocationRolesRead = "warehouse.location-roles.read";

    public const string LocationRolesManage = "warehouse.location-roles.manage";

    public const string OutboundShipmentsRead = "warehouse.outbound-shipments.read";

    /// <summary>S2 (DL-4) — open an outbound with its allocation, and the allocation preview (pack §14.2).</summary>
    public const string OutboundShipmentsCreate = "warehouse.outbound-shipments.create";

    /// <summary>S2 (DL-4) — edit an outbound's header (DL-5: also the dispatch sync).</summary>
    public const string OutboundShipmentsUpdate = "warehouse.outbound-shipments.update";

    /// <summary>S2 (DL-5) — cancel an outbound (reservation release + put-back tasks; pack §8.22).</summary>
    public const string OutboundShipmentsCancel = "warehouse.outbound-shipments.cancel";

    /// <summary>S2 (DL-5) — Packed → ReadyToShip (pack §8.21).</summary>
    public const string OutboundShipmentsRelease = "warehouse.outbound-shipments.release";

    public const string PickTasksRead = "warehouse.pick-tasks.read";

    /// <summary>S2 (DL-5) — assign pick tasks (single + bulk); also the "supervisor" half of the assignee rule (§8.13).</summary>
    public const string PickTasksUpdate = "warehouse.pick-tasks.update";

    public const string PickTasksConfirm = "warehouse.pick-tasks.confirm";

    public const string PickTasksResolve = "warehouse.pick-tasks.resolve";

    public const string PackagesRead = "warehouse.packages.read";

    public const string PackagesCreate = "warehouse.packages.create";

    public const string PackagesUpdate = "warehouse.packages.update";

    /// <summary>S3 (DL-7) — the exception box <c>GET /exceptions</c> (read-only, pack §8.23).</summary>
    public const string ExceptionsRead = "warehouse.exceptions.read";

    /// <summary>S3 (DL-7) — the activity trace <c>GET /activity</c>.</summary>
    public const string ActivityRead = "warehouse.activity.read";

    /// <summary>S3 (DL-7) — the integration monitor <c>GET /integration/status</c>.</summary>
    public const string IntegrationMonitorRead = "warehouse.integration-monitor.read";

    /// <summary>Pack §14.2 — the S1 keys of the namespace.</summary>
    public static readonly IReadOnlyList<string> S1 =
    [
        PutawayTasksRead, PutawayTasksCreate, PutawayTasksUpdate, PutawayTasksConfirm, PutawayTasksCancel, PutawayTasksResolve,
        LocationRolesRead, LocationRolesManage, OutboundShipmentsRead
    ];

    /// <summary>DL-1 — the keys the location-role controller enforces.</summary>
    public static readonly IReadOnlyList<string> Dl1 = [LocationRolesRead, LocationRolesManage];

    /// <summary>DL-4 — the S2 outbound keys the outbound controller adds (create + allocation preview, header edit).</summary>
    public static readonly IReadOnlyList<string> Dl4 = [OutboundShipmentsCreate, OutboundShipmentsUpdate];

    /// <summary>DL-5 — the S2 cancel / release, pick task and package keys (pack §14.2).</summary>
    public static readonly IReadOnlyList<string> Dl5 =
    [
        OutboundShipmentsCancel, OutboundShipmentsRelease, PickTasksRead, PickTasksUpdate, PickTasksConfirm, PickTasksResolve, PackagesRead, PackagesCreate,
        PackagesUpdate
    ];

    /// <summary>DL-7 — the three S3 monitoring read keys (pack §14.2; Tier 1, no action keys — S3 has no button).</summary>
    public static readonly IReadOnlyList<string> Dl7 = [ExceptionsRead, ActivityRead, IntegrationMonitorRead];

    /// <summary>
    /// The keys the controllers enforce so far — exactly what the manifest declares (DL-1 location roles; DL-2 putaway tasks + the frozen outbound
    /// GETs; DL-4 the outbound create / update keys; DL-5 the S2 keys; DL-7 the three S3 read keys).
    /// </summary>
    public static readonly IReadOnlyList<string> Implemented = [.. S1, .. Dl4, .. Dl5, .. Dl7];
}
