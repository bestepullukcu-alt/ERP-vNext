using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.MdmService.Api.ModuleRegistration;

/// <summary>
/// MOD-0289 (Bank Master &amp; Bank Account Registry) — its OWN module, <c>BrandProductMasterManifestProvider</c>'s pattern: a
/// tenant whose plan includes <c>bank-master-bank-account-registry</c> gets these keys through the plan entitlement sync, a
/// tenant without it gets none. Every key here is the verbatim key a controller enforces (BanksController /
/// BankBranchesController). DL-1 declares the bank and branch keys only; the account, sensitive-read and purpose keys come
/// with their own slices. The role templates (viewer / manager) live in Auth (MOD-0018-FU27), not here: nothing is seeded.
/// The module code value is the module's registry slug — the SEC-002 / FU27 (K-1b) value choice is the Treasury Auth
/// annex's to confirm.
/// </summary>
public sealed class BankMasterManifestProvider : IModuleManifestProvider
{
    public const string ModuleCode = "bank-master-bank-account-registry";

    public const string BanksRead = "mdm.banks.read";
    public const string BanksCreate = "mdm.banks.create";
    public const string BanksUpdate = "mdm.banks.update";
    public const string BanksActivate = "mdm.banks.activate";
    public const string BanksDeactivate = "mdm.banks.deactivate";
    public const string BanksDelete = "mdm.banks.delete";
    public const string BanksBulkDelete = "mdm.banks.bulk-delete";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: ModuleCode,
            ModuleName: "BankMasterBankAccountRegistry",
            DisplayName: "Bank Master & Bank Account Registry",
            Domain: "MASTER-DATA-MANAGEMENT",
            Service: "DitenMdmService",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 336,
            Pages:
            [
                // The Banks screen is a later slice (DL-4): until it exists the page stays out of the sidebar; its keys are
                // declared now so the plan sync grants them with the module (the Products page's precedent).
                new ModuleManifestPage(
                    PageCode: "BANKS",
                    DisplayName: "Banks",
                    RoutePath: "/MasterData/Banks",
                    RequiredPermission: BanksRead,
                    ParentPageCode: null,
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("ADD_NEW", "Add New", BanksCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("BULK_DELETE", "Delete Selected Drafts", BanksBulkDelete, "Toolbar", 15, IsDangerous: true, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("VIEW_DETAILS", "View Details", BanksRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("EDIT", "Edit", BanksUpdate, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("ACTIVATE", "Activate", BanksActivate, "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DEACTIVATE", "Deactivate", BanksDeactivate, "RowAction", 50, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete Draft", BanksDelete, "RowAction", 60, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ])
            ],
            Icon: "bx-building",
            IsBaseline: false);
}
