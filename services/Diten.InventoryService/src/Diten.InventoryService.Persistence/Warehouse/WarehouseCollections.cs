namespace Diten.InventoryService.Persistence.Warehouse;

/// <summary>
/// MOD-0178 collections in the InventoryService database (pack §3, §4.6; prefix <c>wms_</c>). None is stock: no document carries an
/// OnHand / Available / Reserved total (pack §2 boundary rule 1) — the stock is the MOD-0173 ledger's. DL-1 collections only; DL-2 adds
/// the task and task-event collections.
/// </summary>
public static class WarehouseCollections
{
    /// <summary>One live record per LOCATION node (roles + accepted storage conditions); soft delete.</summary>
    public const string LocationRoles = "wms_location_roles";

    /// <summary>Insert-only history of the role commands (before / after values, the key events).</summary>
    public const string LocationRoleEvents = "wms_location_role_events";

    /// <summary>The temporary RCS-001 counters (Q-07 B): one per (tenant, company prefix, type), atomic, never reset.</summary>
    public const string NumberCounters = "wms_number_counters";

    public static readonly IReadOnlyList<string> All = [LocationRoles, LocationRoleEvents, NumberCounters];

    /// <summary>DL-2: the tasks (mutable head + embedded confirmations, Version CAS). None keeps a stock total.</summary>
    public const string Tasks = "wms_tasks";

    /// <summary>DL-2: the insert-only task timeline.</summary>
    public const string TaskEvents = "wms_task_events";

    /// <summary>
    /// The collections added by DL-2. Kept apart from <see cref="All"/> because the single-SoR test pins <see cref="All"/> line by line (the
    /// XMC-001 seam of DL-1); the test declares this list on its own new line (the MOD-0177 <c>SinceR2</c> pattern).
    /// </summary>
    public static readonly IReadOnlyList<string> SinceDl2 = [Tasks, TaskEvents];

    /// <summary>DL-4 (S2, pack §4.5): the outbound shipments (mutable head + embedded lines / allocations, Version CAS). No stock total.</summary>
    public const string OutboundShipments = "wms_outbound_shipments";

    /// <summary>DL-4: the insert-only outbound timeline.</summary>
    public const string OutboundEvents = "wms_outbound_events";

    /// <summary>The collections added by DL-4, declared on their own line beside <see cref="SinceDl2"/> (the same single-SoR test pattern).</summary>
    public static readonly IReadOnlyList<string> SinceDl4 = [OutboundShipments, OutboundEvents];

    /// <summary>DL-5 (S2, pack §4.5): the packages (mutable head + embedded contents, Version CAS). No stock total.</summary>
    public const string HandlingUnits = "wms_handling_units";

    /// <summary>DL-5 (pack §8.9): the outbound outbox (<c>OutboundShipmentReady</c>, Pending — the transport is not wired).</summary>
    public const string Outbox = "wms_outbox";

    /// <summary>The collections added by DL-5, declared on their own line beside <see cref="SinceDl4"/> (the same single-SoR test pattern).</summary>
    public static readonly IReadOnlyList<string> SinceDl5 = [HandlingUnits, Outbox];

    public static IEnumerable<string> Everything => All.Concat(SinceDl2).Concat(SinceDl4).Concat(SinceDl5);
}
