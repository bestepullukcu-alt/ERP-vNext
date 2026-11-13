using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.InventoryService.Api.ModuleRegistration;
using Diten.InventoryService.Infrastructure.Authorization;

namespace Diten.InventoryService.Api.WavePlanning;

/// <summary>
/// MOD-0180 Wave Planning self-registration manifest (pack §14): module code <c>WAVE-PLANNING</c> (= the central audit records' source module,
/// <c>WaveCentralAudit.Module</c>), SortOrder 180, sub-namespace <c>warehouse.waves.*</c> ONLY — none of MOD-0178's <c>warehouse.*</c> keys.
/// <para><b>DL-1</b> has no page (the 0178 DL-1 / 0181 DL-1 "sayfasız ilk dilim" pattern): the enforced keys are <c>ApiPermissions</c>. The
/// pages <c>WAVES</c>, <c>WAVEDETAIL</c>, <c>WAVEPLANNER</c>, <c>WAVEEXCEPTIONS</c> (DL-4) and <c>WAVEKPIS</c> (DL-5) take their keys over with
/// their screens; <c>warehouse.waves.settings.configure</c> stays an API permission (no settings screen in v1, Q-6 = B).</para>
/// </summary>
public sealed class WavePlanningManifestProvider : IModuleManifestProvider
{
    // a literal (the Web nav guard reads the manifest SOURCE and resolves literal constants only); the wave architecture tests hold it equal to
    // WaveCentralAudit.Module
    public const string ModuleCode = "WAVE-PLANNING";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: ModuleCode,
            ModuleName: "Wave Planning",
            DisplayName: "Wave Planning",
            Domain: "SUPPLYCHAINEXECUTION",
            Service: "Inventory",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 180,
            Pages: [],
            ApiPermissions:
            [
                new ModuleManifestPermission(WavePermissions.Read, "Read waves", "Tenant"),
                new ModuleManifestPermission(WavePermissions.Create, "Create a wave", "Tenant"),
                new ModuleManifestPermission(WavePermissions.Update, "Edit a draft wave and its orders", "Tenant"),
                new ModuleManifestPermission(WavePermissions.SettingsConfigure, "Configure the warehouse wave setting", "Tenant"),
                new ModuleManifestPermission(WavePermissions.Cancel, "Cancel a draft wave", "Tenant")
            ]);
}
