using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

// G7 (Wave 6.1, owner split 2026-10-18) REMOVED 2026-10-20: the tenant Admin's baseline holds none of the eight S&OP
// (MOD-0190) and Capacity (MOD-0192) keys — they come only through an entitlement; the default Viewer still holds exactly
// the two .read keys (the read rule, never part of G7). Measured through the production template (SelectFor), with the
// catalog shape the manifests produce (Module = the manifest ModuleCode).
public sealed class G7SandopCapacityRoleSplitTests
{
    private static Permission Key(string resource, string action, string module) =>
        new("supplychain", resource, action, $"supplychain.{resource}.{action}", null, moduleOverride: module);

    private static readonly IReadOnlyList<Permission> Catalog =
    [
        Key("sandop-plans", "read", "sop-workflow-signoffs"),
        Key("sandop-plans", "create", "sop-workflow-signoffs"),
        Key("sandop-plans.snapshot", "capture", "sop-workflow-signoffs"),
        Key("sandop-plans.sign-off", "record", "sop-workflow-signoffs"),
        Key("capacity-plans", "read", "capacity-planning"),
        Key("capacity-plans", "create", "capacity-planning"),
        Key("capacity-plans.scenario", "create", "capacity-planning"),
        Key("capacity-plans", "evaluate", "capacity-planning")
    ];

    private static IReadOnlyList<string> Keys(IEnumerable<Permission> perms) =>
        perms.Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();

    [Fact]
    public void Admin_baseline_holds_none_of_the_eight_keys()
    {
        Assert.DoesNotContain("sop-workflow-signoffs", DefaultRolePermissionTemplate.AdminModules);
        Assert.DoesNotContain("capacity-planning", DefaultRolePermissionTemplate.AdminModules);
        Assert.Empty(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.AdminRole, Catalog));
    }

    [Fact]
    public void Viewer_holds_exactly_the_two_read_keys()
    {
        Assert.Equal(new[] { "supplychain.capacity-plans.read", "supplychain.sandop-plans.read" },
            Keys(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.ViewerRole, Catalog)));
    }
}
