using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.SupplyChainService.Infrastructure.Features.SandopPlans;

namespace Diten.SupplyChainService.Api.ModuleRegistration;

/// <summary>
/// MOD-0190 S&amp;OP Workflow &amp; Sign-offs self-registration manifest (pack §24 via the UI-revision patch
/// <c>mvp6-ui-pack-patch-190-192-01</c>; DCP-009 §21). Mirrors the tenant UI (§23, G6): the entry page
/// /SupplyChain/SandopPlans (Create plan + Open plan by ID — no list, F190-LIST) and the plan workspace
/// /SupplyChain/SandopPlans/Details/{sandopPlanId:guid} with Capture snapshot and Record sign-off. Keys are the existing
/// SandopPermissions constants; no new key, page or ID.
/// Ships with the UI in the same change (G4 item C), and is registered only where S&amp;OP is composed (Program.cs, the
/// demand-seed block): a catalog entry on a host whose routes answer 404 would be dead navigation.
/// </summary>
public sealed class SopWorkflowSignoffsManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "sop-workflow-signoffs",
            ModuleName: "SopWorkflowSignoffs",
            DisplayName: "S&OP Workflow & Sign-offs",
            Domain: "SupplyChainExecution",
            Service: "DitenSupplyChainService",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 440,
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "SANDOP_PLANS",
                    DisplayName: "S&OP Plans",
                    RoutePath: "/SupplyChain/SandopPlans",
                    RequiredPermission: SandopPermissions.Read,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Plan", SandopPermissions.Create, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "SANDOP_PLAN_DETAILS",
                    DisplayName: "S&OP Plan",
                    RoutePath: "/SupplyChain/SandopPlans/Details/{sandopPlanId:guid}",
                    RequiredPermission: SandopPermissions.Read,
                    ParentPageCode: "SANDOP_PLANS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 11,
                    Actions:
                    [
                        new ModuleManifestAction("CAPTURE_SNAPSHOT", "Capture Snapshot", SandopPermissions.Capture, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RECORD_SIGN_OFF", "Record Sign-off", SandopPermissions.SignOff, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ])
            ],
            Icon: "bx-check-double",
            IsBaseline: false);
}
