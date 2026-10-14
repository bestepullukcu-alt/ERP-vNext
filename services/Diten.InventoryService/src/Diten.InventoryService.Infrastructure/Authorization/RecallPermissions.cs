namespace Diten.InventoryService.Infrastructure.Authorization;

/// <summary>
/// MOD-0177 permission keys — namespace <c>recall.*</c>, owned by MOD-0177 (pack §14.1; PKS-001 §4 one owner per namespace). Tier-3 verbs
/// registered by the pack (PKS-001 §5): <c>run</c>, <c>cancel</c>, <c>close</c>, <c>complete</c>, <c>confirm</c>, <c>link</c>, <c>manage</c>.
/// All Tenant scope. <see cref="R1"/> are the keys the R1 slice enforces (and the only ones its manifest declares); the rest are
/// enforced from R2–R7.
/// </summary>
public static class RecallPermissions
{
    public const string DashboardRead = "recall.dashboard.read";

    public const string TraceRun = "recall.trace.run";

    public const string TraceExport = "recall.trace.export";

    public const string CasesRead = "recall.cases.read";

    public const string CasesCreate = "recall.cases.create";

    public const string CasesUpdate = "recall.cases.update";

    public const string CasesApprove = "recall.cases.approve";

    public const string CasesCancel = "recall.cases.cancel";

    public const string CasesClose = "recall.cases.close";

    public const string EffectivenessComplete = "recall.effectiveness.complete";

    public const string QuarantineRequestsCreate = "recall.quarantine-requests.create";

    public const string SegregationsConfirm = "recall.segregations.confirm";

    public const string NotificationsCreate = "recall.notifications.create";

    public const string ConsumptionsCreate = "recall.consumptions.create";

    public const string ReturnsLink = "recall.returns.link";

    public const string DrillsRead = "recall.drills.read";

    public const string DrillsManage = "recall.drills.manage";

    public const string DrillsRun = "recall.drills.run";

    public const string EvidenceExport = "recall.evidence.export";

    public const string SettingsRead = "recall.settings.read";

    public const string SettingsManage = "recall.settings.manage";

    public const string LotsStatusRead = "recall.lots.status.read";

    /// <summary>Pack §14.2 — every key of the namespace.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        DashboardRead, TraceRun, TraceExport, CasesRead, CasesCreate, CasesUpdate, CasesApprove, CasesCancel, CasesClose, EffectivenessComplete,
        QuarantineRequestsCreate, SegregationsConfirm, NotificationsCreate, ConsumptionsCreate, ReturnsLink, DrillsRead, DrillsManage, DrillsRun,
        EvidenceExport, SettingsRead, SettingsManage, LotsStatusRead
    ];

    /// <summary>WP-0177-R1 — the keys the R1 controller enforced.</summary>
    public static readonly IReadOnlyList<string> R1Only =
    [
        CasesRead, CasesCreate, CasesUpdate, CasesApprove, CasesCancel, CasesClose, EffectivenessComplete, SegregationsConfirm, NotificationsCreate,
        ConsumptionsCreate, SettingsRead, SettingsManage, LotsStatusRead
    ];

    /// <summary>The keys the controller enforces so far (R1 + R2 trace keys + R3 quarantine-request / return-link keys) — exactly what the manifest declares as API permissions.</summary>
    public static readonly IReadOnlyList<string> Implemented = [.. R1Only, TraceRun, TraceExport, QuarantineRequestsCreate, ReturnsLink];

    /// <summary>
    /// The shared namespace test (<c>ManifestNamespaceOwnershipTests</c>, WP-0177-R1) pins the manifest against <c>R1</c>; under the R2 PARALEL
    /// KURAL that test line may not change, so <c>R1</c> now names the implemented set. The CT renames it to <see cref="Implemented"/> at merge.
    /// </summary>
    public static readonly IReadOnlyList<string> R1 = Implemented;
}
