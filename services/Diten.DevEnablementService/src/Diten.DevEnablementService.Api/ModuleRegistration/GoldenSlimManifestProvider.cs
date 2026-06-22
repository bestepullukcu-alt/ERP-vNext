using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.DevEnablementService.Api.ModuleRegistration;

/// <summary>
/// Golden Slim (DevEnablement) self-registration manifest. Mirrors the ACTUAL code (GoldenReferenceSlimController):
/// one Records page at /GoldenReferenceSlim gated by goldenslim.records.read, with create/update/delete actions →
/// goldenslim.records.{create,update,delete}. Domain/Service are seeded once then operator-owned. (Zero drift to code.)
/// </summary>
public sealed class GoldenSlimManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "GOLDENSLIM",
            ModuleName: "Golden Reference Slim",
            DisplayName: "Golden Slim",
            Domain: "DevEnablement",
            Service: "DevEnablement",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 100,
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "RECORDS",
                    DisplayName: "Records",
                    RoutePath: "/GoldenReferenceSlim",
                    RequiredPermission: "goldenslim.records.read",
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create", "goldenslim.records.create", "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("UPDATE", "Edit", "goldenslim.records.update", "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete", "goldenslim.records.delete", "RowAction", 30, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ])
            ]);
}
