using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

// G7 (Wave 6.1, owner split 2026-10-18): the tenant Admin holds all eight S&OP (MOD-0190) and Capacity (MOD-0192) keys;
// the default Viewer holds exactly the two .read keys. Measured through the production template (SelectFor), with the
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
    public void Admin_holds_all_eight_keys()
    {
        Assert.Contains("sop-workflow-signoffs", DefaultRolePermissionTemplate.AdminModules);
        Assert.Contains("capacity-planning", DefaultRolePermissionTemplate.AdminModules);
        Assert.Equal(Keys(Catalog), Keys(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.AdminRole, Catalog)));
    }

    [Fact]
    public void Viewer_holds_exactly_the_two_read_keys()
    {
        Assert.Equal(new[] { "supplychain.capacity-plans.read", "supplychain.sandop-plans.read" },
            Keys(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.ViewerRole, Catalog)));
    }
}
