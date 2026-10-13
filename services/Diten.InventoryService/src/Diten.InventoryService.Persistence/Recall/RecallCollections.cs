namespace Diten.InventoryService.Persistence.Recall;

/// <summary>
/// MOD-0177 collections in the InventoryService database (pack §3; prefix <c>recall_</c>, the repo's <c>expiry_</c> / <c>quarantine_</c>
/// naming). None is stock: a scope item / declaration quantity is the quantity the record is about. The audit rows go to the
/// service's <c>inventory_audit</c> (AUD-001). R1 collections only; later slices add theirs (trace snapshots R2, quarantine requests and
/// return links R3, drill plans / runs R5, evidence exports R7).
/// </summary>
public static class RecallCollections
{
    public const string Settings = "recall_settings";

    /// <summary>Insert-only.</summary>
    public const string SettingsHistory = "recall_settings_history";

    public const string Cases = "recall_cases";

    /// <summary>Insert-only.</summary>
    public const string CaseEvents = "recall_case_events";

    /// <summary>Draft marks only; frozen once the case leaves Draft.</summary>
    public const string ScopeItems = "recall_scope_items";

    /// <summary>The lots of approved scopes (active until closure); read tenant-wide (CT-R7).</summary>
    public const string LotBlocks = "recall_lot_blocks";

    /// <summary>Insert-only (KR-F).</summary>
    public const string Notifications = "recall_notifications";

    /// <summary>Insert-only.</summary>
    public const string ConsumptionDeclarations = "recall_consumption_declarations";

    /// <summary>Insert-only.</summary>
    public const string Segregations = "recall_segregations";

    /// <summary>Atomic per Tenant + year counters behind <c>RC-{yyyy}-{nnnn}</c> and <c>RC-{yyyy}-D{nnn}</c>.</summary>
    public const string Counters = "recall_counters";

    public static readonly IReadOnlyList<string> All =
        [Settings, SettingsHistory, Cases, CaseEvents, ScopeItems, LotBlocks, Notifications, ConsumptionDeclarations, Segregations, Counters];

    /// <summary>R2 (pack §4.4) — the trace snapshots sealed at submit / approval. Insert-only.</summary>
    public const string TraceSnapshots = "recall_trace_snapshots";

    /// <summary>
    /// The collections added after R1. Kept apart from <see cref="All"/> because the shared single-SoR test pins <see cref="All"/> line by
    /// line and the WP-0177-R2 PARALEL KURAL allows only new lines there (the test declares this list on its own new line).
    /// </summary>
    public static readonly IReadOnlyList<string> SinceR2 = [TraceSnapshots];

    public static IEnumerable<string> Everything => All.Concat(SinceR2);
}
