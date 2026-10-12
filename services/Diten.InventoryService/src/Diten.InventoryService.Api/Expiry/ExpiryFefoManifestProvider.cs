using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.InventoryService.Api.ModuleRegistration;
using Diten.InventoryService.Infrastructure.Authorization;

namespace Diten.InventoryService.Api.Expiry;

/// <summary>
/// MOD-0176 Expiry / FEFO Management self-registration manifest (pack §14.4): module code <c>EXPIRY-FEFO-MANAGEMENT</c>, SortOrder
/// 176, namespace <c>expiry.*</c> ONLY (FU-0174-CAT-01: a foreign key in a manifest moves that key to this module in the catalog).
/// <para><b>WP-0176-F1: NO PAGE YET.</b> The screens come with F4 / F5; a page registered now would put a screen that does not exist into
/// the live tenant's menu (the tenant Admin holds every key). So the F1 keys — exactly the ones the F1 controller enforces — are
/// declared as <c>ApiPermissions</c> (Scope Tenant, module-self-registration standard §2d) until their pages and buttons replace them.
/// The Platform reconcile accepts a page-less manifest (it upserts the pages it is given — none — and syncs the API keys).</para>
/// </summary>
public sealed class ExpiryFefoManifestProvider : IModuleManifestProvider
{
    public const string ModuleCode = "EXPIRY-FEFO-MANAGEMENT";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: ModuleCode,
            ModuleName: "Expiry / FEFO Management",
            DisplayName: "Expiry / FEFO",
            Domain: "SUPPLYCHAINEXECUTION",
            Service: "Inventory",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 176,
            Pages: [],
            Icon: "bx-time-five",
            ApiPermissions:
            [
                new ModuleManifestPermission(ExpiryPermissions.PoliciesRead, "Read the expiry policy", "Tenant"),
                new ModuleManifestPermission(ExpiryPermissions.PoliciesManage, "Change the expiry policy", "Tenant"),
                new ModuleManifestPermission(ExpiryPermissions.ExceptionsRead, "Read FEFO exceptions", "Tenant"),
                new ModuleManifestPermission(ExpiryPermissions.ExceptionsCreate, "Open a FEFO exception", "Tenant"),
                new ModuleManifestPermission(ExpiryPermissions.ExceptionsComment, "Comment on a FEFO exception", "Tenant"),
                new ModuleManifestPermission(ExpiryPermissions.ExceptionsReview, "Take a FEFO exception into review", "Tenant"),
                new ModuleManifestPermission(ExpiryPermissions.ExceptionsResolve, "Resolve a FEFO exception", "Tenant"),
                new ModuleManifestPermission(ExpiryPermissions.ExceptionsClose, "Close a FEFO exception", "Tenant"),
                // WP-0176-F2: createDateOverride (KF-D) — API-only by design (pack §14.2): the lot screens call it, it has no page of its own.
                new ModuleManifestPermission(ExpiryPermissions.DateOverridesCreate, "Prepare a lot-date override", "Tenant")
            ]);
}
