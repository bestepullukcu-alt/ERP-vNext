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

    public static IEnumerable<string> Everything => All.Concat(SinceDl2);
}
