using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.MdmService.Api.ModuleRegistration;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX2 (item 1, CT decision "model A") — Brand and Product master (MOD-0290) as their OWN module.
/// Until FIX2 the Brands page rode on the Product / Item / SKU Master manifest with a foreign permission owner, so a
/// tenant's Admin received the brand keys only by a hand-made grant. As its own module, a tenant whose plan includes
/// <c>brand-product-master</c> gets these keys through the plan entitlement sync like any other module (Admin = all,
/// Viewer = read), and a tenant without it gets none.
/// Every key here is the verbatim key a controller enforces (BrandsController / ProductsController).
/// </summary>
public sealed class BrandProductMasterManifestProvider : IModuleManifestProvider
{
    public const string ModuleCode = "brand-product-master";

    public const string BrandsRead = "mdm.brands.read";
    public const string BrandsCreate = "mdm.brands.create";
    public const string BrandsUpdate = "mdm.brands.update";
    public const string BrandsArchive = "mdm.brands.archive";
    public const string BrandsExport = "mdm.brands.export";
    public const string ProductsRead = "mdm.products.read";
    public const string ProductsCreate = "mdm.products.create";
    public const string ProductsUpdate = "mdm.products.update";
    public const string ProductsArchive = "mdm.products.archive";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: ModuleCode,
            ModuleName: "BrandProductMaster",
            DisplayName: "Brand & Product Master",
            Domain: "MASTER-DATA-MANAGEMENT",
            Service: "DitenMdmService",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 335,
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "BRANDS",
                    DisplayName: "Brands",
                    RoutePath: "/MasterData/Brands",
                    RequiredPermission: BrandsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 5,
                    Actions:
                    [
                        new ModuleManifestAction("ADD_NEW", "Add New", BrandsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EXPORT", "Export", BrandsExport, "Toolbar", 15, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("VIEW_DETAILS", "View Details", BrandsRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("EDIT", "Edit", BrandsUpdate, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("ARCHIVE", "Archive", BrandsArchive, "RowAction", 40, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),
                // The Products screen was never in the sidebar; it stays out of it (no navigation change in this round).
                // Its keys are declared here so the plan sync grants them with the module.
                new ModuleManifestPage(
                    PageCode: "PRODUCTS",
                    DisplayName: "Products",
                    RoutePath: "/MasterData/Products",
                    RequiredPermission: ProductsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 6,
                    Actions:
                    [
                        new ModuleManifestAction("ADD_NEW", "Add New", ProductsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("VIEW_DETAILS", "View Details", ProductsRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("EDIT", "Edit", ProductsUpdate, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("ARCHIVE", "Archive", ProductsArchive, "RowAction", 40, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ])
            ],
            Icon: "bx-purchase-tag",
            IsBaseline: false);
}
