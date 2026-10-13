using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.SupplyChainService.Infrastructure.Features.Returns;

namespace Diten.SupplyChainService.Api.ModuleRegistration;

/// <summary>
/// MOD-0186 Reverse Logistics self-registration manifest (pack §33; DCP-009 §21). Built by R-2 (2026-10-04) from the Q160
/// draft overlay v2.
/// Mirrors the Returns tenant UI (pack §32): one List page at /SupplyChain/Returns gated by supplychain.returns.read, with
/// the create toolbar action and the seven transition row actions. Permission keys are the existing ReturnPermissions
/// constants and the values of ReturnPermissions.ForTarget (D5 = A; ReturnPermissions.cs, accepted source
/// normal-source.tar.gz edb759a0…5a21); no new key, page or ID. supplychain.shipments.read (G-SHIPREAD) stays a
/// conjunction the adapter and backend enforce; it is Shipments' key and reaches Auth through Shipments' manifest.
/// R-2 (owner decision 2026-10-04): supplychain.returns.transition is declared as the CHANGE_STATUS row action. Pack §33 M-03
/// had it API-only, but the Platform→Auth sync carries only keys a manifest declares, so it never reached Auth's catalogue
/// (measured: 8 of 9 Returns keys present) and no role could hold it, while ReturnsController.cs:20 requires it on every
/// transition — every transition was 403 for every user.
/// Ships with the Returns UI only (DCP-009 §21.4): the AddSingleton line and the Nav.Module/Nav.Page keys are applied by
/// the integration owner from _shared-integration/ in the same change.
/// </summary>
public sealed class ReverseLogisticsManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "reverse-logistics",
            ModuleName: "ReverseLogistics",
            DisplayName: "Reverse Logistics",
            Domain: "SupplyChainExecution",
            Service: "DitenSupplyChainService",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 420,
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "RETURNS",
                    DisplayName: "Returns",
                    RoutePath: "/SupplyChain/Returns",
                    RequiredPermission: ReturnPermissions.Read,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Return", ReturnPermissions.Create, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("CHANGE_STATUS", "Change Return Status", ReturnPermissions.Transition, "RowAction", 15, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("AUTHORIZE", "Authorize", ReturnPermissions.ForTarget("Authorized")!, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("REJECT", "Reject", ReturnPermissions.ForTarget("Rejected")!, "RowAction", 30, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("MARK_IN_TRANSIT", "Mark In Transit", ReturnPermissions.ForTarget("InTransit")!, "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("CANCEL", "Cancel Return", ReturnPermissions.ForTarget("Cancelled")!, "RowAction", 50, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("RECEIVE", "Receive (manual assertion)", ReturnPermissions.ForTarget("Received")!, "RowAction", 60, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DISPOSITION", "Disposition", ReturnPermissions.ForTarget("Dispositioned")!, "RowAction", 70, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("CLOSE", "Close", ReturnPermissions.ForTarget("Closed")!, "RowAction", 80, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ])
            ],
            Icon: "bx-undo",
            IsBaseline: false);
}
