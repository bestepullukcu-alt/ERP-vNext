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

    /// <summary>R3 (pack §8.4 KR-B) — one request to MOD-0175 per included Stock node of an approved revision (mutable head, Version).</summary>
    public const string QuarantineRequests = "recall_quarantine_requests";

    /// <summary>R3 (pack §8.6 KR-H) — CUSTOMER_RETURNs linked to a case. Insert-only.</summary>
    public const string ReturnLinks = "recall_return_links";

    /// <summary>The R3 collections (declared beside <see cref="SinceR2"/> by the single-SoR test).</summary>
    public static readonly IReadOnlyList<string> SinceR3 = [QuarantineRequests, ReturnLinks];

    /// <summary>R4 (CT-R16, RECALL 1.3.0 prepareSettingsChange) — prepared settings changes, the MOD-0022 subject recall.settings-change. Insert-only.</summary>
    public const string SettingsChanges = "recall_settings_changes";

    /// <summary>The R4 collections (declared beside <see cref="SinceR3"/> by the single-SoR test).</summary>
    public static readonly IReadOnlyList<string> SinceR4 = [SettingsChanges];

    public static IEnumerable<string> Everything => All.Concat(SinceR2).Concat(SinceR3).Concat(SinceR4);
}
