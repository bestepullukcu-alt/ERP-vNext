using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.ESignature.SelfRegistration;

/// <summary>
/// MOD-0022 — the e-Signature module's self-registration manifest (pack §14, CT-D8 / CT-D10).
///
/// <para>CT-D8 (b): its OWN nav group "e-İmza" holds every page, the admin pages too (permission-gated) — NOT pages in the
/// tenant-settings manifest, because the catalog sync re-attributes a key's module to the last manifest that declares it
/// (FU-0174-CAT-01). So every key here is <c>esign.*</c> and no other manifest declares one (namespace test).</para>
///
/// <para>KS-0: every route is a tenant-shell route; none starts with <c>/Platform</c>, so <c>ScopeFromRoute</c> classifies
/// every key Tenant, and the two API-only keys pass <c>Scope: "Tenant"</c> explicitly.</para>
///
/// <para>CT-D10: baseline module (entitlement-free). Default role grants for <c>esign.signatures.sign</c> and
/// <c>esign.my-samples.manage</c> live in AuthService's role template — outside this slice (E2 evidence, OPEN).</para>
///
/// <para>⚠ E2 SHIPS NO SCREENS (they are E5). The pages are declared now so their keys reach the catalog, but
/// <see cref="PagesNavigable"/> keeps them out of the tenant menu until E5 builds the routes — a nav entry to a route
/// that does not exist yet would be a dead link in every tenant shell.</para>
/// </summary>
public sealed class ESignManifestProvider : IModuleManifestProvider
{
    public const string ModuleCode = "esign";

    /// <summary>E5 flips this to true together with the Web pages (pack §14 table: every page nav-visible).</summary>
    public const bool PagesNavigable = false;

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: ModuleCode,
            ModuleName: "e-Signature",
            DisplayName: "e-İmza",
            Domain: "PlatformSharedServices",
            Service: "DitenPlatform",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 860,
            Icon: "bx-pen",
            IsBaseline: true,
            ApiPermissions:
            [
                new ModuleManifestPermission(ESignPermissions.SignaturesSign, "Sign (e-signature dialog)", "Tenant"),
                new ModuleManifestPermission(ESignPermissions.RequestsCreate, "Create signature requests (consumer modules)", "Tenant")
            ],
            Pages:
            [
                Page("ESIGN_REQUESTS", "Signature Requests", "/ESignature/Requests", ESignPermissions.RequestsRead, 10,
                [
                    Row("SIGNREQUEST", "Sign", ESignPermissions.SignaturesSign, 10),
                    Row("REJECTREQUEST", "Reject", ESignPermissions.RequestsReject, 20),
                    Row("OPENRECORD", "Open record", ESignPermissions.RequestsRead, 30)
                ]),
                Page("ESIGN_VERIFICATION", "Signature Verification", "/ESignature/Verification", ESignPermissions.SignaturesRead, 20,
                [
                    Toolbar("REVERIFY", "Re-verify", ESignPermissions.SignaturesVerify, 10),
                    Row("INVALIDATE", "Invalidate signature", ESignPermissions.SignaturesInvalidate, 20, dangerous: true)
                ]),
                Page("ESIGN_AUDITEXPORT", "Signature Audit Export", "/ESignature/AuditExport", ESignPermissions.SignaturesExport, 30,
                [
                    Toolbar("EXPORTPDF", "Export as PDF", ESignPermissions.SignaturesExport, 10),
                    Toolbar("EXPORTCSV", "Export as CSV", ESignPermissions.SignaturesExport, 20)
                ]),
                Page("ESIGN_MYSIGNATURE", "My Signature", "/ESignature/MySignature", ESignPermissions.MySamplesManage, 40,
                [
                    Toolbar("SAVESAMPLE", "Save with password", ESignPermissions.MySamplesManage, 10),
                    Row("DEACTIVATESAMPLE", "Deactivate", ESignPermissions.MySamplesManage, 20),
                    Row("WITHDRAWSAMPLE", "Withdraw", ESignPermissions.MySamplesManage, 30, dangerous: true)
                ]),
                Page("ESIGN_POLICY", "e-Signature Policy", "/ESignature/Settings/Policy", ESignPermissions.PolicyRead, 50,
                [
                    Toolbar("SAVEPOLICY", "Save with reason", ESignPermissions.PolicyManage, 10),
                    Toolbar("CREATEOVERRIDE", "Copy and edit for company", ESignPermissions.PolicyManage, 20),
                    Toolbar("REVERTOVERRIDE", "Revert to tenant default", ESignPermissions.PolicyManage, 30, dangerous: true)
                ]),
                Page("ESIGN_MEANINGS", "Signature Meanings", "/ESignature/Settings/Meanings", ESignPermissions.MeaningsRead, 60,
                [
                    Toolbar("NEWMEANING", "New meaning", ESignPermissions.MeaningsManage, 10),
                    Row("EDITMEANING", "Edit", ESignPermissions.MeaningsManage, 20),
                    Row("TOGGLEMEANING", "Activate / deactivate", ESignPermissions.MeaningsManage, 30)
                ]),
                Page("ESIGN_SAMPLEAPPROVALS", "Signature Sample Approvals", "/ESignature/Settings/SampleApprovals", ESignPermissions.SamplesApprove, 70,
                [
                    Row("APPROVESAMPLE", "Approve", ESignPermissions.SamplesApprove, 10),
                    Row("REJECTSAMPLE", "Reject", ESignPermissions.SamplesReject, 20)
                ])
            ]);

    private static ModuleManifestPage Page(string code, string name, string route, string permission, int sort,
        IReadOnlyList<ModuleManifestAction> actions) =>
        new(code, name, route, permission, null, PagesNavigable, "List", sort, actions);

    private static ModuleManifestAction Toolbar(string code, string name, string permission, int sort, bool dangerous = false) =>
        new(code, name, permission, "Toolbar", sort, dangerous, IsToolbarAction: true, IsRowAction: false);

    private static ModuleManifestAction Row(string code, string name, string permission, int sort, bool dangerous = false) =>
        new(code, name, permission, "RowAction", sort, dangerous, IsToolbarAction: false, IsRowAction: true);
}
