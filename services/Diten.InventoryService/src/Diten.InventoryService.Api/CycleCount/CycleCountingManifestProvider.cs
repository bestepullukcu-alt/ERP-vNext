using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Api.ModuleRegistration;

namespace Diten.InventoryService.Api.CycleCounting;

/// <summary>
/// MOD-0181 Cycle Counting self-registration manifest (pack §14, Q-16 = A): module code <c>CYCLE-COUNTING</c> (= the central audit records'
/// source module, <c>CycleCountCentralAudit.Module</c>), SortOrder 181, namespace <c>cycle-count.*</c> ONLY.
/// <para><b>DL-1</b> has no page (the 0176 F1 / 0177 R1 / 0178 DL-1 "sayfasız ilk dilim" pattern): the enforced keys are <c>ApiPermissions</c>.
/// The pages <c>CYCLECOUNTS</c>, <c>COUNTSHEET</c>, <c>COUNTVARIANCES</c> (DL-4) and <c>COUNTEVIDENCE</c> (DL-5) take their keys over with their
/// screens; <c>cycle-count.settings.*</c> stay API permissions (no settings screen, F-11).</para>
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
            Pages: [],
            ApiPermissions:
            [
                new ModuleManifestPermission(CycleCountPermissions.CountsRead, "Read cycle counts", "Tenant"),
                new ModuleManifestPermission(CycleCountPermissions.CountsCreate, "Open a cycle count", "Tenant"),
                new ModuleManifestPermission(CycleCountPermissions.CountsUpdate, "Edit and release a cycle count", "Tenant"),
                new ModuleManifestPermission(CycleCountPermissions.CountsBulkDelete, "Delete draft cycle counts in bulk", "Tenant"),
                new ModuleManifestPermission(CycleCountPermissions.CountsCancel, "Cancel a cycle count", "Tenant"),
                new ModuleManifestPermission(CycleCountPermissions.CountsRecord, "Enter counted quantities, add found stock and submit a count", "Tenant")
            ]);
}
