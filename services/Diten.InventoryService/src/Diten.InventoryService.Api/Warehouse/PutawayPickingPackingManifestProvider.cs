using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.InventoryService.Api.ModuleRegistration;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Infrastructure.Authorization;

namespace Diten.InventoryService.Api.Warehouse;

/// <summary>
/// MOD-0178 Putaway / Picking / Packing self-registration manifest (pack §14.4): module code <c>PUTAWAY-PICKING-PACKING</c> (= the central
/// audit records' source module, <see cref="WarehouseCentralAudit.Module"/>), SortOrder 178, namespace <c>warehouse.*</c> ONLY (FU-0174-CAT-01).
/// <para><b>DL-1: NO PAGE YET</b> (the 0176 F1 / 0177 R1 "sayfasız ilk dilim" pattern). The screens <c>PUTAWAYTASKS</c> and
/// <c>WAREHOUSELOCATIONROLES</c> come with DL-3; a page registered now would put a screen that does not exist into the tenant's menu. So the
/// keys the DL-1 controller enforces are declared as <c>ApiPermissions</c> (Scope Tenant) until their page and buttons replace them.</para>
/// </summary>
public sealed class PutawayPickingPackingManifestProvider : IModuleManifestProvider
{
    public const string ModuleCode = WarehouseCentralAudit.Module;

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: ModuleCode,
            ModuleName: "Putaway / Picking / Packing",
            DisplayName: "Warehouse",
            Domain: "SUPPLYCHAINEXECUTION",
            Service: "Inventory",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 178,
            Pages: [],
            Icon: "bx-package",
            ApiPermissions:
            [
                new ModuleManifestPermission(WarehousePermissions.LocationRolesRead, "Read warehouse location roles", "Tenant"),
                new ModuleManifestPermission(WarehousePermissions.LocationRolesManage, "Create, edit and delete warehouse location roles", "Tenant")
            ]);
}
