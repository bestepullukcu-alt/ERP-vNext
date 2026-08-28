using System.Text.RegularExpressions;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// MOD-0029-FU29 — Permission / RBAC Seed Hardening. Pins the FU06–FU23 Document Control governance permission
/// keys into the canonical AuthService seed and asserts the escalation boundary is preserved (platform-scoped →
/// SuperAdmin only; tenant Admin/Viewer never auto-gain them). Source-based tests mirror the existing
/// <see cref="DocumentManagementPermissionSeedTests"/> style so they stay deterministic and Mongo-free.
/// </summary>
public sealed class Mod0029Fu29PermissionSeedHardeningTests
{
    // (resource, action) for every FU06–FU23 governance key added by FU29. module is always "platform"
    // (PermissionScope.PlatformAdmin), so the composed Key is "platform.{resource}.{action}". Each triple is the
    // VERBATIM Diten.Platform DocumentManagement*Permissions.* constant value (no drift with the enforced contract).
    public static readonly (string Resource, string Action)[] GovernanceKeys =
    {
        // FU06 — Master Register
        ("document-management.master-register", "view"),
        ("document-management.master-register", "manage"),
        ("document-management.master-register", "link"),
        ("document-management.master-register", "audit.view"),
        // FU07 — Identifiers
        ("document-management.identifiers", "view"),
        ("document-management.identifiers", "allocate"),
        ("document-management.identifiers", "reserve"),
        ("document-management.identifiers", "cancel"),
        // FU08/FU08A — Lifecycle
        ("document-management.master-register.lifecycle", "view"),
        ("document-management.master-register.lifecycle", "manage"),
        // FU09 — Approval Routes
        ("document-management.master-register.approval", "view"),
        ("document-management.master-register.approval", "manage"),
        ("document-management.master-register.approval.evidence", "record"),
        // FU10 — Release Gates
        ("document-management.master-register.release-gate", "view"),
        ("document-management.master-register.release-gate", "evaluate"),
        ("document-management.master-register.release-gate.evidence", "record"),
        // FU11 — Training
        ("document-management.master-register.training", "view"),
        ("document-management.master-register.training", "manage"),
        ("document-management.master-register.training", "verify"),
        // FU12 — Periodic Review
        ("document-management.master-register.periodic-review", "view"),
        ("document-management.master-register.periodic-review", "manage"),
        ("document-management.master-register.periodic-review", "approve-extension"),
        ("document-management.master-register.periodic-review.escalation", "view"),
        // FU13 — Suspension / Retirement
        ("document-management.master-register.suspension", "view"),
        ("document-management.master-register.suspension", "manage"),
        ("document-management.master-register.suspension", "approve"),
        ("document-management.master-register.retirement", "approve"),
        // FU14 — External Documents
        ("document-management.external-documents", "view"),
        ("document-management.external-documents", "manage"),
        ("document-management.external-documents.monitoring", "record"),
        ("document-management.external-documents.impact", "manage"),
        // FU15 — Retention / Legal Hold / Disposition
        ("document-management.retention", "view"),
        ("document-management.retention", "manage"),
        ("document-management.legal-hold", "view"),
        ("document-management.legal-hold", "manage"),
        ("document-management.legal-hold", "release"),
        ("document-management.disposition", "manage"),
        ("document-management.disposition", "approve"),
        // FU16 — Repository Assessment
        ("document-management.repository-assessment", "view"),
        ("document-management.repository-assessment", "manage"),
        ("document-management.repository-assessment", "approve"),
        // FU17 — Controlled Copy
        ("document-management.master-register.controlled-copy", "view"),
        ("document-management.master-register.controlled-copy", "manage"),
        ("document-management.master-register.controlled-copy", "reconcile"),
        // FU18 — Variant Localization
        ("document-management.template-variants.localization", "view"),
        ("document-management.template-variants.localization", "manage"),
        ("document-management.template-variants.translation-review", "record"),
        ("document-management.template-variants.local-approval", "record"),
        // FU20 — Downtime
        ("document-management.downtime", "view"),
        ("document-management.downtime", "manage"),
        ("document-management.downtime", "temporary-issue"),
        ("document-management.downtime", "reconcile"),
        // FU21 — GDocP Corrections
        ("document-management.gdocp-corrections", "view"),
        ("document-management.gdocp-corrections", "record"),
        ("document-management.gdocp-corrections", "review"),
        ("document-management.gdocp-correction-policies", "manage"),
        // FU22 — Quality Events / Deviations / CAPA
        ("document-management.quality-events", "view"),
        ("document-management.quality-events", "manage"),
        ("document-management.deviations", "view"),
        ("document-management.deviations", "manage"),
        ("document-management.capa", "view"),
        ("document-management.capa", "manage"),
        ("document-management.quality-bridge", "manage"),
        // FU23 — Signatures
        ("document-management.signatures", "view"),
        ("document-management.signatures", "request"),
        ("document-management.signatures", "sign"),
        ("document-management.signatures", "verify"),
        ("document-management.signatures", "invalidate"),
        ("document-management.signature-policies", "manage"),
    };

    public static IEnumerable<object[]> GovernanceKeyCases =>
        GovernanceKeys.Select(k => new object[] { k.Resource, k.Action });

    // ── 1. Document_management_governance_permissions_are_seeded ────────────────────────────────
    [Theory]
    [MemberData(nameof(GovernanceKeyCases))]
    public void Document_management_governance_permission_is_seeded(string resource, string action)
    {
        var seederSource = ReadSeederSource();
        var constructor = $"\"platform\", \"{resource}\", \"{action}\"";

        Assert.Contains(constructor, seederSource, StringComparison.Ordinal);
    }

    // ── 4. New_permissions_follow_platform_document_management_prefix ────────────────────────────
    [Fact]
    public void All_governance_keys_follow_platform_document_management_prefix()
    {
        foreach (var (resource, action) in GovernanceKeys)
        {
            var key = $"platform.{resource}.{action}";
            Assert.StartsWith("platform.document-management.", key, StringComparison.Ordinal);
        }
    }

    // ── 5. New_permissions_have_resource_and_action_mapping ─────────────────────────────────────
    [Theory]
    [MemberData(nameof(GovernanceKeyCases))]
    public void Governance_key_maps_to_resource_and_action(string resource, string action)
    {
        var permission = new Permission("platform", resource, action, "x", "d");

        Assert.Equal($"platform.{resource}.{action}".ToLowerInvariant(), permission.Key);
        Assert.Equal(resource, permission.Resource);
        Assert.Equal(action, permission.Action);
    }

    // ── 6. SuperAdmin catalog contains new permissions (full-catalog convention) ─────────────────
    [Fact]
    public void SuperAdmin_receives_every_governance_key()
    {
        var catalog = GovernanceCatalog();

        var keys = DefaultRolePermissionTemplate.SelectFor("SuperAdmin", catalog).Select(p => p.Key).ToHashSet();

        foreach (var p in catalog)
        {
            Assert.Contains(p.Key, keys);
        }
    }

    // ── 7. Tenant_default_roles_do_not_unexpectedly_gain_platform_governance_permissions ────────
    [Fact]
    public void Tenant_Admin_gains_no_governance_key()
    {
        var catalog = GovernanceCatalog();

        var keys = DefaultRolePermissionTemplate.SelectFor("Admin", catalog).Select(p => p.Key).ToList();

        Assert.Empty(keys);
    }

    [Fact]
    public void Tenant_Viewer_gains_no_governance_key()
    {
        var catalog = GovernanceCatalog();

        var keys = DefaultRolePermissionTemplate.SelectFor("Viewer", catalog).Select(p => p.Key).ToList();

        Assert.Empty(keys);
    }

    [Fact]
    public void Governance_keys_are_platform_scoped_and_not_tenant_assignable()
    {
        foreach (var (resource, action) in GovernanceKeys)
        {
            var permission = new Permission("platform", resource, action, "x", "d");
            Assert.Equal(PermissionScope.PlatformAdmin, permission.Scope);
            Assert.False(DefaultRolePermissionTemplate.IsTenantAssignable(permission),
                $"{permission.Key} must not be tenant-assignable (escalation boundary).");
        }
    }

    // ── 3. No_duplicate_permission_keys (whole seed catalog) ────────────────────────────────────
    [Fact]
    public void Seed_catalog_has_no_duplicate_permission_keys()
    {
        var keys = ExtractSeededPermissionKeys();

        var duplicates = keys
            .GroupBy(k => k, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, $"Duplicate permission keys in seed: {string.Join(", ", duplicates)}");
    }

    // ── 10–18. Critical high-privilege operations have a dedicated seeded key ────────────────────
    [Theory]
    [InlineData("platform.document-management.legal-hold.release")]        // FU15 legal hold release
    [InlineData("platform.document-management.disposition.manage")]        // FU15 disposition execute-marker
    [InlineData("platform.document-management.gdocp-corrections.review")]  // FU21 correction review/reject
    [InlineData("platform.document-management.quality-events.manage")]     // FU22 quality event close/cancel
    [InlineData("platform.document-management.deviations.manage")]         // FU22 deviation close/cancel
    [InlineData("platform.document-management.capa.manage")]               // FU22 CAPA effectiveness/close/cancel
    [InlineData("platform.document-management.signatures.sign")]           // FU23 signature sign
    [InlineData("platform.document-management.signatures.verify")]         // FU23 signature verify
    [InlineData("platform.document-management.signatures.invalidate")]     // FU23 signature invalidate
    [InlineData("platform.document-management.downtime.temporary-issue")]  // FU20 temporary issue approve/reconcile
    [InlineData("platform.document-management.downtime.reconcile")]        // FU20 temporary issue reconcile
    [InlineData("platform.document-management.external-documents.impact.manage")] // FU14 impact complete
    [InlineData("platform.document-management.retention.manage")]          // FU15 retention policy activate/retire
    public void Critical_operation_key_is_present_in_seed_catalog(string key)
    {
        Assert.Contains(key, ExtractSeededPermissionKeys());
    }

    // ── 8. Existing_document_management_permissions_remain_seeded (regression sample) ────────────
    [Theory]
    [InlineData("platform.document-management.controlled-documents.view")]
    [InlineData("platform.document-management.controlled-documents.create")]
    [InlineData("platform.document-management.template-masters.view")]
    [InlineData("platform.document-management.template-variants.view")]
    [InlineData("platform.document-management.access.manage")]
    public void Existing_document_management_permission_remains_seeded(string key)
    {
        Assert.Contains(key, ExtractSeededPermissionKeys());
    }

    // ── 19. Permission_descriptions_not_empty (governance keys) ──────────────────────────────────
    [Fact]
    public void Governance_permission_constructors_have_non_empty_display_and_description()
    {
        var source = ReadSeederSource();

        // Match: new("platform", "<resource>", "<action>", "<display>", "<description>"
        foreach (var (resource, action) in GovernanceKeys)
        {
            var pattern = "new\\(\"platform\", \"" + Regex.Escape(resource) + "\", \"" + Regex.Escape(action) +
                          "\", \"(?<display>[^\"]+)\", \"(?<desc>[^\"]+)\"";
            var match = Regex.Match(source, pattern);

            Assert.True(match.Success, $"Governance key platform.{resource}.{action} not found with display+description.");
            Assert.False(string.IsNullOrWhiteSpace(match.Groups["display"].Value));
            Assert.False(string.IsNullOrWhiteSpace(match.Groups["desc"].Value));
        }
    }

    private static List<Permission> GovernanceCatalog() =>
        GovernanceKeys.Select(k => new Permission("platform", k.Resource, k.Action, "x", "d")).ToList();

    // Extracts every seeded permission Key by parsing the three leading string args of each Permission constructor
    // (new("module", "resource", "action", ...)) in DataSeeder.cs and composing "module.resource.action" (lowercased,
    // matching Permission.Key). Non-permission `new(...)` calls do not have three consecutive string literals.
    private static List<string> ExtractSeededPermissionKeys()
    {
        var source = ReadSeederSource();
        var matches = Regex.Matches(source, "new\\(\"(?<m>[^\"]+)\",\\s*\"(?<r>[^\"]+)\",\\s*\"(?<a>[^\"]+)\"");
        return matches
            .Select(m => $"{m.Groups["m"].Value}.{m.Groups["r"].Value}.{m.Groups["a"].Value}".ToLowerInvariant())
            .ToList();
    }

    private static string ReadSeederSource() => File.ReadAllText(GetDataSeederPath());

    private static string GetDataSeederPath()
    {
        var directory = Path.GetDirectoryName(typeof(DataSeeder).Assembly.Location)
            ?? throw new InvalidOperationException("Unable to resolve DataSeeder assembly directory.");

        while (directory is not null)
        {
            var candidate = Path.Combine(directory, "Seed", "DataSeeder.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        var repoCandidate = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Diten.AuthService.Persistence", "Seed", "DataSeeder.cs"));

        if (File.Exists(repoCandidate))
        {
            return repoCandidate;
        }

        // Location-independent fallback: walk up from the (possibly relocated, e.g. -o output) base directory and
        // probe the known repo-relative seed path. Keeps the source-based tests runnable from any build output dir.
        var probe = new DirectoryInfo(AppContext.BaseDirectory);
        var relative = Path.Combine("services", "Diten.AuthService", "src",
            "Diten.AuthService.Persistence", "Seed", "DataSeeder.cs");
        while (probe is not null)
        {
            var candidate = Path.Combine(probe.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            probe = probe.Parent;
        }

        throw new FileNotFoundException("DataSeeder.cs could not be found.");
    }
}
