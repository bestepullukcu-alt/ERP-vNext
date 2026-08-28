using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.Crm.SelfRegistration;

/// <summary>
/// MOD-0149 Customer 360 / Account Hierarchy — Commercial Suite CRM module self-registration manifest.
/// <para>Reconciles the code-owned identity of the CRM catalog entry (ModuleCode <c>CRM</c>) and its page/permission
/// descriptors. An operator had already created the <c>CRM</c> catalog item manually (Origin=Manual,
/// IsTenantAssignable=true); pushing this manifest flips it to Origin=SelfRegistered while PRESERVING every SOFT
/// operator field (Domain/Service/DisplayName/SortOrder/IsTenantAssignable) per the reconcile ownership rules — so
/// tenant-assignability and the operator's taxonomy choices are untouched.</para>
/// <para><b>Navigation:</b> the Accounts page is registered with <c>IsNavigationVisible = false</c> on purpose.
/// The tenant shell still renders Accounts from the hardcoded <c>_LayoutTenantShell</c> block (gated by the same
/// <c>crm.account.read</c> key); making the descriptor nav-visible while that static entry exists would double-render
/// it under the MOD-0285 DynamicModuleMenu. Flipping this to <c>true</c> and removing the static &lt;li&gt; is the
/// MOD-0285 data-driven-navigation migration (tracked as a follow-up). The descriptor itself — route + required
/// permission — is correct and now exists, and its permission is synced to the AuthService catalog.</para>
/// <para>This provider lives in Platform.Application (like the Organization/Workflow cross-service providers); it does
/// NOT add a manifest push inside CrmService, and it declares no Account business capability.</para>
/// </summary>
public sealed class CrmManifestProvider : IModuleManifestProvider
{
    // Verbatim MOD-0018 permission keys the tenant-shell UX guard and CrmService [HasPermission] both enforce.
    private const string AccountsRead = "crm.account.read";
    private const string ContactsRead = "crm.contact.read";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "CRM",
            ModuleName: "CRM",
            DisplayName: "CRM",
            Domain: "Sales", // SOFT: only seeds on first-register; the existing item's operator Domain is preserved.
            Service: "DitenCrmService", // SOFT: preserved for the existing item (operator may correct the legacy value).
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true, // SOFT: preserved for the existing item (already true).
            SortOrder: 60,
            Icon: "bx-buildings",
            IsBaseline: false, // HARD: CRM is a licensed module — tenant entitlement required (never entitlement-free).
            Pages:
            [
                // Not nav-visible yet — static tenant-shell menu owns the nav until the MOD-0285 migration (see class doc).
                new ModuleManifestPage("ACCOUNTS", "Accounts", "/CRM/Accounts", AccountsRead, null, false, "List", 10, []),
                // MOD-0150 FU02 — Contacts page descriptor (nav-visible=false; static menu owns nav until MOD-0285).
                new ModuleManifestPage("CONTACTS", "Contacts", "/CRM/Contacts", ContactsRead, null, false, "List", 20, [])
            ]);
}
