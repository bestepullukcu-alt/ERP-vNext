using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.MdmService.Api.ModuleRegistration;

/// <summary>
/// Product / Item / SKU Master registration descriptor for the Global Product and Finished Good tenant surfaces.
/// The manifest declares only the read and create permission candidates enforced by their API controllers.
/// </summary>
public sealed class ProductItemSkuMasterManifestProvider : IModuleManifestProvider
{
    private const string Read = "mdm.global-products.read";
    private const string Create = "mdm.global-products.create";
    private const string FinishedGoodsRead = "mdm.finished-goods.read";
    private const string FinishedGoodsCreate = "mdm.finished-goods.create";
    private const string GskusRead = "mdm.gskus.read";
    private const string GskusCreate = "mdm.gskus.create";
    private const string LskusRead = "mdm.lskus.read";
    private const string LskusCreate = "mdm.lskus.create";
    private const string ProductAbbreviationsRead = "mdm.product-abbreviations.read";
    private const string ProductAbbreviationsRequest = "mdm.product-abbreviations.request";
    private const string ProductAbbreviationsCancel = "mdm.product-abbreviations.cancel";
    private const string ProductAbbreviationsApprove = "mdm.product-abbreviations.approve";
    private const string ProductAbbreviationsReject = "mdm.product-abbreviations.reject";
    private const string ProductAbbreviationsCorrect = "mdm.product-abbreviations.correct";
    private const string ProductAbbreviationsRetire = "mdm.product-abbreviations.retire";
    private const string ProductAbbreviationsAudit = "mdm.product-abbreviations.audit";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "product-item-sku-master",
            ModuleName: "ProductItemSkuMaster",
            DisplayName: "Product / Item / SKU Master",
            Domain: "MasterDataManagement",
            Service: "DitenMdmService",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 340,
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "GLOBAL_PRODUCTS",
                    DisplayName: "Global Products",
                    RoutePath: "/MasterDataManagement/GlobalProducts",
                    RequiredPermission: Read,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("ADD_NEW", "Add New", Create, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("VIEW_DETAILS", "View Details", Read, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "FINISHED_GOODS",
                    DisplayName: "Finished Goods",
                    RoutePath: "/MasterDataManagement/FinishedGoods",
                    RequiredPermission: FinishedGoodsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 20,
                    Actions:
                    [
                        new ModuleManifestAction("ADD_NEW", "Add New", FinishedGoodsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("VIEW_DETAILS", "View Details", FinishedGoodsRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "GSKUS",
                    DisplayName: "GSKUs",
                    RoutePath: "/MasterDataManagement/Gskus",
                    RequiredPermission: GskusRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 30,
                    Actions:
                    [
                        new ModuleManifestAction("ADD_NEW", "Add New", GskusCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("VIEW_DETAILS", "View Details", GskusRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "LSKUS",
                    DisplayName: "LSKUs",
                    RoutePath: "/MasterDataManagement/Lskus",
                    RequiredPermission: LskusRead,
                    ParentPageCode: null,
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 40,
                    Actions:
                    [
                        new ModuleManifestAction("ADD_NEW", "Add New", LskusCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("VIEW_DETAILS", "View Details", LskusRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "PRODUCT_ABBREVIATIONS",
                    DisplayName: "Product Abbreviations",
                    RoutePath: "/MDM/ProductAbbreviationRegister",
                    RequiredPermission: ProductAbbreviationsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 50,
                    Actions:
                    [
                        new ModuleManifestAction("VIEW_DETAILS", "View Details", ProductAbbreviationsRead, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("REQUEST", "Request", ProductAbbreviationsRequest, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("CANCEL", "Cancel", ProductAbbreviationsCancel, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("APPROVE", "Approve", ProductAbbreviationsApprove, "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("REJECT", "Reject", ProductAbbreviationsReject, "RowAction", 50, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("CORRECT", "Correct", ProductAbbreviationsCorrect, "RowAction", 60, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("RETIRE", "Retire", ProductAbbreviationsRetire, "RowAction", 70, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("VIEW_AUDIT", "View Audit", ProductAbbreviationsAudit, "RowAction", 80, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),

                // MOD-0290-FU02 Brand master — post main-sync catalogue registration (2026-08-28). Brand lives in
                // this module (Brand + Product both landed in MDM MOD-0290). The hand-written CRM tenant-shell link
                // it used to share was removed with main's MOD-0285 DynamicModuleMenu adoption, so this descriptor is
                // now the only source of the sidebar entry. RequiredPermission is the verbatim key BrandsController enforces.
                new ModuleManifestPage(
                    PageCode: "BRANDS",
                    DisplayName: "Brands",
                    RoutePath: "/MasterData/Brands",
                    RequiredPermission: "mdm.brands.read",
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 5,
                    Actions: [])
            ],
            Icon: "bx-package",
            IsBaseline: false);
}
