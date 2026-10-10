using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.SupplyChainService.Infrastructure.Features.Claims;

namespace Diten.SupplyChainService.Api.ModuleRegistration;

/// <summary>
/// MOD-0187 Claims Management self-registration manifest (pack §33; DCP-009 §21). Built by R-4c from the September draft v4.
/// Mirrors the Claims tenant UI (pack §32): one List page at /SupplyChain/Claims gated by supplychain.claims.read, with
/// the create toolbar action and the six transition row actions. Permission keys are the existing ClaimPermissions
/// constants (ClaimPermissions.cs:4-8, accepted archive normal-source.tar.gz edb759a0…); no new key, page or ID.
/// The G-SHIPREAD conjunction (supplychain.shipments.read) cannot be expressed in the single-key action model
/// (DCP-009 §21.5 item 4); the adapter and backend keep enforcing it.
/// Ships with the Claims UI only (DCP-009 §21.4): its AddSingleton line and the Nav.Module/Nav.Page keys landed in the same
/// change (R-4c). Every enforced Claims key is declared here: investigate, decide and settle are enforced on the transition
/// endpoint (ClaimsController.Transition's ClaimPermission, read by ClaimContextMiddleware:61) and, per target, in
/// TransitionClaimHandler:13 through ClaimWire.Permission (ClaimModels.cs:45); they are declared as the six row actions.
/// </summary>
public sealed class ClaimsManagementManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "claims-management",
            ModuleName: "ClaimsManagement",
            DisplayName: "Claims Management",
            Domain: "SupplyChainExecution",
            Service: "DitenSupplyChainService",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 430,
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "SHIPMENT_CLAIMS",
                    DisplayName: "Claims",
                    RoutePath: "/SupplyChain/Claims",
                    RequiredPermission: ClaimPermissions.Read,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Claim", ClaimPermissions.Create, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("INVESTIGATE", "Investigate", ClaimPermissions.Investigate, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("WITHDRAW", "Withdraw", ClaimPermissions.Investigate, "RowAction", 30, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("APPROVE", "Approve", ClaimPermissions.Decide, "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("REJECT", "Reject", ClaimPermissions.Decide, "RowAction", 50, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("SETTLE", "Settle (no payment posted)", ClaimPermissions.Settle, "RowAction", 60, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("CLOSE", "Close", ClaimPermissions.Decide, "RowAction", 70, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ])
            ],
            Icon: "bx-receipt",
            IsBaseline: false);
}
