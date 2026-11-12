using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Api.ModuleRegistration;

namespace Diten.InventoryService.Api.CycleCounting;

/// <summary>
/// MOD-0181 Cycle Counting self-registration manifest (pack §14, Q-16 = A): module code <c>CYCLE-COUNTING</c> (= the central audit records'
/// source module, <c>CycleCountCentralAudit.Module</c>), SortOrder 181, namespace <c>cycle-count.*</c> ONLY.
/// <para><b>DL-1 … DL-3</b> had no page (the "sayfasız ilk dilim" pattern): every enforced key was an <c>ApiPermissions</c> entry.</para>
/// <para><b>DL-4</b> (pack §8.13, §11, §14; AC-41, AC-49): the three screens — <c>CYCLECOUNTS</c> (nav; Cycle Count Dashboard), <c>COUNTSHEET</c>
/// (not in the menu; the count sheet, reached from a count) and <c>COUNTVARIANCES</c> (nav; Variance Trace) — with one action per real button; the
/// Draft row action is "İptal" (<c>counts.cancel</c>; no "Sil", no <c>counts.delete</c> — G-0181-01). Their keys leave <c>ApiPermissions</c>, which
/// is now exactly the screenless settings pair (F-11).</para>
/// <para><b>DL-5</b> (pack §8.13, F-14; AC-50): <c>COUNTEVIDENCE</c> (nav; Evidence Pack) — the export (<c>evidence.export</c>) and the download
/// (<c>evidence.read</c>, the page key).</para>
/// </summary>
public sealed class CycleCountingManifestProvider : IModuleManifestProvider
{
    // a literal (the Web nav guard reads the manifest SOURCE and resolves literal constants only);
    // CycleCountArchitectureTests.AC7_the_manifest_declares_exactly_the_cycle_count_keys_the_controller_enforces holds it equal to
    // CycleCountCentralAudit.Module
    public const string ModuleCode = "CYCLE-COUNTING";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: ModuleCode,
            ModuleName: "Cycle Counting",
            DisplayName: "Cycle Counting",
            Domain: "SUPPLYCHAINEXECUTION",
            Service: "Inventory",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 181,
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "CYCLECOUNTS",
                    DisplayName: "Cycle counts",
                    RoutePath: "/Warehouse/CycleCounts",
                    RequiredPermission: CycleCountPermissions.CountsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("CYCLE_COUNT_CREATE", "New cycle count", CycleCountPermissions.CountsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("CYCLE_COUNT_UPDATE", "Edit cycle count", CycleCountPermissions.CountsUpdate, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("CYCLE_COUNT_RELEASE", "Start cycle count", CycleCountPermissions.CountsUpdate, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("CYCLE_COUNT_CANCEL", "Cancel cycle count", CycleCountPermissions.CountsCancel, "RowAction", 40, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("CYCLE_COUNT_BULK_DELETE", "Delete selected draft counts", CycleCountPermissions.CountsBulkDelete, "BulkAction", 50, IsDangerous: true, IsToolbarAction: true, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "COUNTSHEET",
                    DisplayName: "Count sheet",
                    RoutePath: "/Warehouse/CycleCounts/Sheet/{countId}",
                    RequiredPermission: CycleCountPermissions.CountsRead,
                    ParentPageCode: "CYCLECOUNTS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 20,
                    Actions:
                    [
                        new ModuleManifestAction("COUNT_LINE_RECORD", "Enter counted quantity", CycleCountPermissions.CountsRecord, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("COUNT_LINE_ADD", "Add found stock", CycleCountPermissions.CountsRecord, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("CYCLE_COUNT_SUBMIT", "Submit count", CycleCountPermissions.CountsRecord, "Toolbar", 30, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "COUNTVARIANCES",
                    DisplayName: "Count variances",
                    RoutePath: "/Warehouse/CountVariances",
                    RequiredPermission: CycleCountPermissions.VariancesRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 30,
                    Actions:
                    [
                        new ModuleManifestAction("COUNT_VARIANCE_APPROVE", "Approve variance", CycleCountPermissions.VariancesApprove, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("COUNT_VARIANCE_REJECT", "Reject variance", CycleCountPermissions.VariancesReject, "RowAction", 20, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("COUNT_VARIANCE_RECOUNT", "Request recount", CycleCountPermissions.VariancesRecount, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("COUNT_ADJUSTMENT_RETRY", "Retry correction", CycleCountPermissions.VariancesPost, "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("COUNT_ADJUSTMENT_RESOLVE_INTEGRITY", "Resolve integrity error", CycleCountPermissions.VariancesResolve, "RowAction", 50, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "COUNTEVIDENCE",
                    DisplayName: "Count evidence",
                    RoutePath: "/Warehouse/CountEvidenceExports",
                    RequiredPermission: CycleCountPermissions.EvidenceRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 40,
                    Actions:
                    [
                        new ModuleManifestAction("COUNT_EVIDENCE_EXPORT", "Export evidence (PDF / CSV)", CycleCountPermissions.EvidenceExport, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("COUNT_EVIDENCE_DOWNLOAD", "Download evidence file", CycleCountPermissions.EvidenceRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ])
            ],
            ApiPermissions:
            [
                new ModuleManifestPermission(CycleCountPermissions.SettingsRead, "Read the count settings", "Tenant"),
                new ModuleManifestPermission(CycleCountPermissions.SettingsConfigure, "Configure the count settings", "Tenant")
            ]);
}
