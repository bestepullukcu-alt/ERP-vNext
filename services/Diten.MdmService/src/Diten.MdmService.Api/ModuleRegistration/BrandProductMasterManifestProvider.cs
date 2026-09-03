using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.MdmService.Api.ModuleRegistration;

/// <summary>
/// Brand / Product Master registration descriptor. Permission ownership remains with
/// <c>brand-product-master</c>; Product / Item / SKU consumes these records by reference.
/// </summary>
public sealed class BrandProductMasterManifestProvider : IModuleManifestProvider
{
    private const string BrandsRead = "mdm.brands.read";
    private const string BrandsCreate = "mdm.brands.create";
    private const string BrandsUpdate = "mdm.brands.update";
    private const string BrandsArchive = "mdm.brands.archive";
    private const string ProductsRead = "mdm.products.read";
    private const string ProductsCreate = "mdm.products.create";
    private const string ProductsUpdate = "mdm.products.update";
    private const string ProductsArchive = "mdm.products.archive";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "brand-product-master",
            ModuleName: "BrandProductMaster",
            DisplayName: "Brand / Product Master",
            Domain: "MASTER-DATA-MANAGEMENT",
            Service: "DitenMdmService",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 330,
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
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("ADD_NEW", "Add New", BrandsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("VIEW_DETAILS", "View Details", BrandsRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("EDIT", "Edit", BrandsUpdate, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("ARCHIVE", "Archive", BrandsArchive, "RowAction", 40, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "PRODUCTS",
                    DisplayName: "Products",
                    RoutePath: "/MasterData/Products",
                    RequiredPermission: ProductsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 20,
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
