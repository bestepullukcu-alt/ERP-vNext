using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.ManufacturingService.Infrastructure.Authorization;

namespace Diten.ManufacturingService.Api.ModuleRegistration;

/// <summary>
/// MOD-0193 BOM &amp; Routings self-registration manifest. Her uygulanan anahtar burada bildirilir: <c>read</c> sayfa
/// izni, diğer dördü eylem anahtarı — Platform yalnız bildirilen anahtarları Auth'a eşitler (bildirilmeyen bir anahtarın
/// ucu herkese sonsuza dek 403 olur). Sayfa yolu Web adaptörünün rotasıdır (<c>/Manufacturing/Boms</c>).
/// </summary>
public sealed class BomRoutingsManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "bom-routings",
            ModuleName: "BomRoutings",
            DisplayName: "BOM & Routings",
            Domain: "SupplyChainExecution",
            Service: "Manufacturing",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 193,
            Icon: "bx-git-repo-forked",
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "BOMS",
                    DisplayName: "Bills of Materials",
                    RoutePath: "/Manufacturing/Boms",
                    RequiredPermission: BomPermissions.Read,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create draft", BomPermissions.Create, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EDIT", "Edit draft", BomPermissions.Update, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("RELEASE", "Release", BomPermissions.Release, "RowAction", 30, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete draft", BomPermissions.Delete, "RowAction", 40, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ])
            ]);
}
