using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.SupplyChainService.Infrastructure.Features.SandopPlans;

namespace Diten.SupplyChainService.Api.ModuleRegistration;

/// <summary>
/// MOD-0190 S&amp;OP Workflow &amp; Sign-offs self-registration manifest (pack §24; DCP-009 §21). DRAFT overlay — not built.
/// Mirrors the S&amp;OP tenant UI (pack §23.4): the entry page at /SupplyChain/SandopPlans (navigation-visible; "List" page
/// type without a table, F190-LIST) with the Create toolbar action, and the plan workspace detail page with the Capture
/// Snapshot and Record Sign-off toolbar actions. Permission keys are the existing SandopPermissions constants
/// (SandopPermissions.cs:3 in the accepted isolated source 8fa00d40…b745); no new key, page or ID.
/// Ships with the S&amp;OP UI only (SR-D4 / DCP-009 §21.4): the AddSingleton line and the Nav.Module/Nav.Page keys are
/// applied by the integration owner from _shared-integration/ in the same change.
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
