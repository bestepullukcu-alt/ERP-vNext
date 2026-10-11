using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.SupplyChainService.Infrastructure.Features.Returns;

namespace Diten.SupplyChainService.Api.ModuleRegistration;

/// <summary>
/// MOD-0186 Reverse Logistics self-registration manifest (pack §33; DCP-009 §21). DRAFT overlay — not built.
/// Mirrors the Returns tenant UI (pack §32): one List page at /SupplyChain/Returns gated by supplychain.returns.read, with
/// the create toolbar action and the seven transition row actions. Permission keys are the existing ReturnPermissions
/// constants and the values of ReturnPermissions.ForTarget (D5 = A; ReturnPermissions.cs, accepted source
/// normal-source.tar.gz edb759a0…5a21); no new key, page or ID. The conjunctions supplychain.returns.transition (every
/// row action) and supplychain.shipments.read (create and every row action, G-SHIPREAD) cannot be expressed in the
/// single-key action model (pack §33 open gap 3); the declared key is the target key, and the adapter and backend keep
/// enforcing the conjunctions.
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
