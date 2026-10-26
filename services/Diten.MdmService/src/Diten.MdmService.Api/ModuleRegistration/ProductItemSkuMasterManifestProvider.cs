using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.MdmService.Api.ModuleRegistration;

/// <summary>
/// Product / Item / SKU Master registration descriptor for the tenant surfaces owned by this module.
/// The manifest declares only permission candidates enforced by their API controllers.
/// </summary>
public sealed class ProductItemSkuMasterManifestProvider : IModuleManifestProvider
{
    private const string Read = "mdm.global-products.read";
    private const string Create = "mdm.global-products.create";
    private const string Update = "mdm.global-products.update";
    private const string Submit = "mdm.global-products.submit";
    private const string Withdraw = "mdm.global-products.withdraw";
    private const string Retire = "mdm.global-products.retire";
    private const string RequestCorrection = "mdm.global-products.request-correction";
    private const string RequestRetirement = "mdm.global-products.request-retirement";
    // MOD-0290-FU05 slice 2 (owner Karar a): the approved product's brand change.
    private const string UpdateBrand = "mdm.global-products.update-brand";
    private const string FinishedGoodsRead = "mdm.finished-goods.read";
    private const string FinishedGoodsCreate = "mdm.finished-goods.create";
    private const string FinishedGoodsSubmit = "mdm.finished-goods.submit";
    private const string FinishedGoodsRetire = "mdm.finished-goods.retire";
    private const string GskusRead = "mdm.gskus.read";
    private const string GskusCreate = "mdm.gskus.create";
    private const string LskusRead = "mdm.lskus.read";
    private const string LskusCreate = "mdm.lskus.create";
    private const string LskusSubmit = "mdm.lskus.submit";
    private const string LskusWithdraw = "mdm.lskus.withdraw";
    private const string LskusRequestRetirement = "mdm.lskus.request-retirement";
    private const string LskusRetire = "mdm.lskus.retire";
    private const string ProductAbbreviationsRead = "mdm.product-abbreviations.read";
    private const string ProductAbbreviationsRequest = "mdm.product-abbreviations.request";
    private const string ProductAbbreviationsCancel = "mdm.product-abbreviations.cancel";
    private const string ProductAbbreviationsApprove = "mdm.product-abbreviations.approve";
    private const string ProductAbbreviationsReject = "mdm.product-abbreviations.reject";
    private const string ProductAbbreviationsCorrect = "mdm.product-abbreviations.correct";
    private const string ProductAbbreviationsRetire = "mdm.product-abbreviations.retire";
    private const string ProductAbbreviationsAudit = "mdm.product-abbreviations.audit";
    private const string ProductScopesRead = "mdm.product-legal-entity-scopes.read";
    private const string ProductScopesConfigure = "mdm.product-legal-entity-scopes.configure";
    private const string ProductScopesReplace = "mdm.product-legal-entity-scopes.replace";
    private const string ProductScopesEnd = "mdm.product-legal-entity-scopes.end";
    private const string ProductScopeRolloutActivate = "mdm.product-legal-entity-scope-rollout.activate";
    private const string ProductScopeRolloutRollback = "mdm.product-legal-entity-scope-rollout.rollback";
    private const string ProductIdentityLifecycleOperationRecover =
        "mdm.product-identity.lifecycle-operations.recover";

    // MOD-0290-FU04 S5 (§23 #7): the Items page's keys (ItemsController). S2 lane merge FIX1: + the approval's withdraw key.
    private const string ItemsRead = "mdm.items.read";
    private const string ItemsCreate = "mdm.items.create";
    private const string ItemsUpdate = "mdm.items.update";
    private const string ItemsSubmit = "mdm.items.submit";
    private const string ItemsDeactivate = "mdm.items.deactivate";
    private const string ItemsRetire = "mdm.items.retire";
    private const string ItemsWithdraw = "mdm.items.withdraw";

    // S5-FIX1: EDIT_STOCK_BEHAVIOUR is a FORM action (the quick view's "Stock behaviour" section opens its own offcanvas;
    // the row menu only offers it as the STOCK_FLAGS_REQUIRED shortcut), not a row action of its own.
    // S5 (O-12, CT decision 5): the stock-behaviour edit is held TODAY by the keys StockBehaviourPermissions names; its
    // own keys (mdm.<level>.edit-stock-behaviour) come in a separate work item (BL-573) — a new mdm.gskus.* key would
    // break Auth's exact lifecycle key set.
    private const string GskusEditStockBehaviour = "mdm.gskus.update";
    private const string LskusEditStockBehaviour = "mdm.lskus.create";
    private const string FinishedGoodsEditStockBehaviour = "mdm.finished-goods.create";

    // WP-VW-W4 (W4-BE-b2, main) — competitor products.
    private const string CompetitorProductsRead = "mdm.competitor-products.read";
    private const string CompetitorProductsManage = "mdm.competitor-products.manage";
    private const string CompetitorProductsPropose = "mdm.competitor-products.propose";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "product-item-sku-master",
            ModuleName: "ProductItemSkuMaster",
            DisplayName: "Product / Item / SKU Master",
            Domain: "MASTER-DATA-MANAGEMENT",
            Service: "DitenMdmService",
            // S5: + the ITEMS page and the EDIT_STOCK_BEHAVIOUR actions. S2 lane merge FIX1 (1.2.0): + ITEMS WITHDRAW_APPROVAL.
            // MOD-0290-FU05 slice 2 (1.3.0): + GLOBAL_PRODUCTS CHANGE_BRAND (mdm.global-products.update-brand).
            ModuleVersion: "1.3.0",
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
                        new ModuleManifestAction("VIEW_DETAILS", "View Details", Read, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("EDIT", "Edit", Update, "RowAction", 25, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("SUBMIT", "Submit", Submit, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("WITHDRAW_APPROVAL", "Withdraw Approval", Withdraw, "RowAction", 35, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("REQUEST_CORRECTION", "Request Correction", RequestCorrection, "RowAction", 37, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("CHANGE_BRAND", "Change Brand", UpdateBrand, "RowAction", 38, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("REQUEST_RETIREMENT", "Request Retirement", RequestRetirement, "RowAction", 40, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("RETIRE", "Retire", Retire, "System", 41, IsDangerous: true, IsToolbarAction: false, IsRowAction: false),
                        new ModuleManifestAction(
                            "RECOVER_ORPHANED_LIFECYCLE_OPERATION",
                            "Recover Orphaned Lifecycle Operation",
                            ProductIdentityLifecycleOperationRecover,
                            "System",
                            50,
                            IsDangerous: true,
                            IsToolbarAction: false,
                            IsRowAction: false)
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
                        new ModuleManifestAction("VIEW_DETAILS", "View Details", FinishedGoodsRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        // FG FIX1 item 9 (CT decision B): SUBMIT stays declared without an endpoint in v1 (Auth's exact
                        // key set depends on it; a key no door enforces opens nothing). RETIRE is a system act, as for
                        // GSKU / LSKU — never a human's (Auth: non-human key). Both get their endpoints in the FG
                        // lifecycle round.
                        new ModuleManifestAction("SUBMIT", "Submit", FinishedGoodsSubmit, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("RETIRE", "Retire", FinishedGoodsRetire, "System", 40, IsDangerous: true, IsToolbarAction: false, IsRowAction: false),
                        new ModuleManifestAction("EDIT_STOCK_BEHAVIOUR", "Edit Stock Behaviour", FinishedGoodsEditStockBehaviour, "FormAction", 50, IsDangerous: false, IsToolbarAction: false, IsRowAction: false)
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
                        new ModuleManifestAction("VIEW_DETAILS", "View Details", GskusRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("EDIT", "Edit Draft", "mdm.gskus.update", "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("SUBMIT", "Submit Identity", "mdm.gskus.submit", "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("WITHDRAW_APPROVAL", "Withdraw Approval", "mdm.gskus.withdraw", "RowAction", 50, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("REQUEST_CORRECTION", "Request Correction", "mdm.gskus.request-correction", "RowAction", 60, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("REQUEST_RETIREMENT", "Request Retirement", "mdm.gskus.request-retirement", "RowAction", 70, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("RETIRE", "Retire Identity", "mdm.gskus.retire", "System", 80, IsDangerous: true, IsToolbarAction: false, IsRowAction: false),
                        new ModuleManifestAction("EDIT_STOCK_BEHAVIOUR", "Edit Stock Behaviour", GskusEditStockBehaviour, "FormAction", 90, IsDangerous: false, IsToolbarAction: false, IsRowAction: false)
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
                        new ModuleManifestAction("VIEW_DETAILS", "View Details", LskusRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("SUBMIT", "Submit", LskusSubmit, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("WITHDRAW_APPROVAL", "Withdraw Approval", LskusWithdraw, "RowAction", 35, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("REQUEST_RETIREMENT", "Request Retirement", LskusRequestRetirement, "RowAction", 37, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("RETIRE", "Retire", LskusRetire, "System", 40, IsDangerous: true, IsToolbarAction: false, IsRowAction: false),
                        new ModuleManifestAction("EDIT_STOCK_BEHAVIOUR", "Edit Stock Behaviour", LskusEditStockBehaviour, "FormAction", 50, IsDangerous: false, IsToolbarAction: false, IsRowAction: false)
                    ]),
                // MOD-0290-FU04 S5 (§23 #1, #7): the Items page under Master Data, for holders of mdm.items.read.
                // WITHDRAW_APPROVAL arrives with S2 (CT decision 4), declared at the S2 lane merge FIX1.
                new ModuleManifestPage(
                    PageCode: "ITEMS",
                    DisplayName: "Items",
                    RoutePath: "/MasterDataManagement/Items",
                    RequiredPermission: ItemsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 45,
                    Actions:
                    [
                        new ModuleManifestAction("ADD_NEW", "Add New", ItemsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("VIEW_DETAILS", "View Details", ItemsRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("EDIT", "Edit", ItemsUpdate, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("SUBMIT", "Submit", ItemsSubmit, "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        // S2 lane merge FIX1: the GP / GSKU / LSKU pattern — WITHDRAW_APPROVAL right after SUBMIT, not dangerous
                        // (it returns the item to Draft / Deactivated; it destroys nothing and may be submitted again).
                        new ModuleManifestAction("WITHDRAW_APPROVAL", "Withdraw Approval", ItemsWithdraw, "RowAction", 45, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DEACTIVATE", "Deactivate", ItemsDeactivate, "RowAction", 50, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("REACTIVATE", "Reactivate", ItemsSubmit, "RowAction", 60, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("RETIRE", "Retire", ItemsRetire, "RowAction", 70, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
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
                // WP-MDM-BRAND-ACCEPT-01 FIX2 (item 1, CT model A) — the BRANDS page moved to its own module
                // (BrandProductMasterManifestProvider, brand-product-master); this module declares no brand key.
                new ModuleManifestPage(
                    PageCode: "PRODUCT_LEGAL_ENTITY_SCOPES",
                    DisplayName: "Product Legal Entity Scopes",
                    RoutePath: "/MasterDataManagement/ProductLegalEntityScopes",
                    RequiredPermission: ProductScopesRead,
                    ParentPageCode: null,
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 60,
                    Actions:
                    [
                        new ModuleManifestAction("VIEW_DETAILS", "View Details", ProductScopesRead, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("CONFIGURE", "Configure", ProductScopesConfigure, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("REPLACE", "Replace", ProductScopesReplace, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("END", "End", ProductScopesEnd, "RowAction", 40, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("ACTIVATE_ROLLOUT", "Activate Rollout", ProductScopeRolloutActivate, "Operator", 50, IsDangerous: true, IsToolbarAction: false, IsRowAction: false),
                        new ModuleManifestAction("ROLLBACK_ROLLOUT", "Rollback Rollout", ProductScopeRolloutRollback, "Operator", 60, IsDangerous: true, IsToolbarAction: false, IsRowAction: false)
                    ]),

                // WP-VW-W4 (W4-BE-b2, §2.7) — competitor products (a separate MOD-0290 record; the marketing / medical team's
                // screen — WEB-c draws it). RequiredPermission + the actions are the verbatim keys CompetitorProductsController
                // enforces: read the list, manage (direct writes + the proposal decision), propose (a rep's "not in the list"
                // from the visit report — declared here so the key is part of this module's set).
                new ModuleManifestPage(
                    PageCode: "COMPETITOR_PRODUCTS",
                    DisplayName: "Competitor Products",
                    RoutePath: "/MasterDataManagement/CompetitorProducts",
                    RequiredPermission: CompetitorProductsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 15,
                    Actions:
                    [
                        new ModuleManifestAction("ADD_NEW", "Add New", CompetitorProductsManage, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("VIEW_DETAILS", "View Details", CompetitorProductsRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("PROPOSE", "Propose Competitor", CompetitorProductsPropose, "Action", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: false)
                    ])
            ],
            Icon: "bx-package",
            IsBaseline: false);
}
