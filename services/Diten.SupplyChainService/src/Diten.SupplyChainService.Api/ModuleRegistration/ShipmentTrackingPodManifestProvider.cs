using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.SupplyChainService.Api.ModuleRegistration;

public sealed class ShipmentTrackingPodManifestProvider : IModuleManifestProvider
{
    private const string Read = "supplychain.shipments.read";
    private const string Create = "supplychain.shipments.create";
    private const string Dispatch = "supplychain.shipments.dispatch";
    private const string Cancel = "supplychain.shipments.cancel";
    private const string CapturePod = "supplychain.shipments.pod.capture";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "shipment-tracking-pod",
            ModuleName: "ShipmentTrackingPod",
            DisplayName: "Shipment Tracking & POD",
            Domain: "SupplyChainExecution",
            Service: "DitenSupplyChainService",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 390,
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "SHIPMENTS",
                    DisplayName: "Shipments",
                    RoutePath: "/SupplyChain/Shipments",
                    RequiredPermission: Read,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions: []),
                new ModuleManifestPage(
                    PageCode: "SHIPMENT_CREATE",
                    DisplayName: "Create Shipment",
                    RoutePath: "/SupplyChain/Shipments/Create",
                    RequiredPermission: Create,
                    ParentPageCode: "SHIPMENTS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 11,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE", "Save", Create, "Toolbar", 10, false, true, false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "SHIPMENT_DETAILS",
                    DisplayName: "Shipment Details",
                    RoutePath: "/SupplyChain/Shipments/Details/{shipmentId:guid}",
                    RequiredPermission: Read,
                    ParentPageCode: "SHIPMENTS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 12,
                    Actions:
                    [
                        new ModuleManifestAction("CHANGE_STATUS", "Change Status", Dispatch, "Toolbar", 10, false, true, false),
                        new ModuleManifestAction("CANCEL", "Cancel Shipment", Cancel, "Toolbar", 20, true, true, false),
                        new ModuleManifestAction("CAPTURE_POD", "Capture POD", CapturePod, "Toolbar", 30, false, true, false)
                    ])
            ],
            Icon: "bx-package",
            IsBaseline: false);
}
