using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

/// <summary>
/// MOD-0022 CT-D10 — the default tenant roles can sign. Admin holds the whole esign.* namespace (module "esign", incl. sample
/// approval — CT-D5); Viewer holds exactly its read keys plus <c>esign.signatures.sign</c> and <c>esign.my-samples.manage</c>.
/// Everything a role held before is unchanged: the selection over a catalog WITHOUT the esign keys is identical before and
/// after the esign keys are added.
/// </summary>
public sealed class ESignDefaultRoleGrantTests
{
    // The 15 esign.* keys as the Platform manifest registers them (Module = ModuleCode "esign", Scope Tenant).
    private static readonly (string Resource, string Action)[] ESignKeys =
    [
        ("signatures", "read"), ("signatures", "sign"), ("signatures", "verify"), ("signatures", "invalidate"), ("signatures", "export"),
        ("requests", "read"), ("requests", "create"), ("requests", "reject"),
        ("my-samples", "manage"), ("samples", "approve"), ("samples", "reject"),
        ("policy", "read"), ("policy", "manage"), ("meanings", "read"), ("meanings", "manage")
    ];

    private static List<Permission> BaseCatalog() =>
    [
        new("auth", "users", "read", "Read User", null, moduleOverride: "access-governance"),
        new("auth", "users", "create", "Create User", null, moduleOverride: "access-governance"),
        new("mdm", "legal-entities", "read", "Read Legal Entity", null, moduleOverride: "legal-entity"),
        new("platform", "tenant-security", "manage", "Manage Tenant Security", null),
        new("platform", "tenants", "read", "Read Tenant", null),
        new("platform", "positions", "read", "Read Positions", null, moduleOverride: "organization"),
        new("platform", "positions", "manage", "Manage Positions", null, moduleOverride: "organization"),
        new("mdm", "gskus", "read", "Read GSKU", null, moduleOverride: "mdm-gsku")
    ];

    private static List<Permission> ESignCatalog() =>
        ESignKeys.Select(k => new Permission("esign", k.Resource, k.Action, $"{k.Resource} {k.Action}", null, moduleOverride: "esign")).ToList();

    private static List<string> Keys(string role, IEnumerable<Permission> catalog) =>
        DefaultRolePermissionTemplate.SelectFor(role, catalog).Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();

    [Fact]
    public void Every_esign_key_is_tenant_scoped_and_tenant_assignable()
    {
        Assert.All(ESignCatalog(), p =>
        {
            Assert.Equal(PermissionScope.Tenant, p.Scope);
            Assert.True(DefaultRolePermissionTemplate.IsTenantAssignable(p));
        });
    }

    [Fact]
    public void Admin_holds_the_whole_esign_namespace_including_sample_approval()
    {
        var admin = Keys(DefaultRolePermissionTemplate.AdminRole, BaseCatalog().Concat(ESignCatalog()));
        var esign = admin.Where(k => k.StartsWith("esign.", StringComparison.Ordinal)).ToList();
        Assert.Equal(ESignCatalog().Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToList(), esign);
        Assert.Contains("esign.samples.approve", esign);
        Assert.Contains("esign.policy.manage", esign);
    }

    [Fact]
    public void Viewer_holds_exactly_the_esign_reads_plus_sign_and_my_samples()
    {
        var viewer = Keys(DefaultRolePermissionTemplate.ViewerRole, BaseCatalog().Concat(ESignCatalog()));
        Assert.Equal(
            new[]
            {
                "esign.meanings.read", "esign.my-samples.manage", "esign.policy.read", "esign.requests.read",
                "esign.signatures.read", "esign.signatures.sign"
            },
            viewer.Where(k => k.StartsWith("esign.", StringComparison.Ordinal)).ToArray());
    }

    [Theory]
    [InlineData("esign.policy.manage")]
    [InlineData("esign.meanings.manage")]
    [InlineData("esign.samples.approve")]
    [InlineData("esign.samples.reject")]
    [InlineData("esign.signatures.invalidate")]
    [InlineData("esign.signatures.export")]
    [InlineData("esign.signatures.verify")]
    [InlineData("esign.requests.create")]
    [InlineData("esign.requests.reject")]
    public void No_management_key_reaches_the_viewer(string key)
    {
        Assert.DoesNotContain(key, Keys(DefaultRolePermissionTemplate.ViewerRole, BaseCatalog().Concat(ESignCatalog())));
        Assert.DoesNotContain(key, DefaultRolePermissionTemplate.DefaultSignerPermissions);
    }

    [Fact]
    public void The_signer_set_is_exactly_sign_and_my_samples()
    {
        Assert.Equal(
            new[] { "esign.my-samples.manage", "esign.signatures.sign" },
            DefaultRolePermissionTemplate.DefaultSignerPermissions.OrderBy(k => k, StringComparer.Ordinal).ToArray());
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Viewer")]
    [InlineData("SuperAdmin")]
    public void Every_non_esign_grant_of_a_default_role_is_unchanged(string role)
    {
        var before = Keys(role, BaseCatalog());
        var after = Keys(role, BaseCatalog().Concat(ESignCatalog())).Where(k => !k.StartsWith("esign.", StringComparison.Ordinal)).ToList();
        Assert.Equal(before, after);
    }

    [Fact]
    public void A_signer_key_registered_under_PlatformAdmin_scope_would_not_reach_the_viewer()
    {
        // The signer set only ever widens Tenant-scoped keys: a same-named key classified PlatformAdmin stays out.
        var platformSign = new Permission("esign", "signatures", "sign", "x", null, moduleOverride: "esign", scope: PermissionScope.PlatformAdmin);
        Assert.Empty(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.ViewerRole, [platformSign]));
    }
}
