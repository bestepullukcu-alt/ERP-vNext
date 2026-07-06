using System.Linq;
using System.Reflection;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.API.Security;
using Xunit;

namespace Diten.Platform.Application.Tests.Navigation;

// FIX-MENU-SETTINGS-ACCESS — broken-access-control fix. The tenant-WIDE menu preference read/write must be gated
// behind platform.tenant-navigation.manage, while GET menu (a user rendering its OWN menu) stays open. These tests
// pin the gate PLACEMENT via reflection so the enforcement (the shared, already-tested HasPermissionAttribute
// filter) cannot silently regress to the pre-fix "every tenant_user may write the whole tenant's sidebar" state.
public sealed class NavigationControllerAuthorizationTests
{
    private const string ManageKey = "platform.tenant-navigation.manage";

    private static MethodInfo Action(string name) =>
        typeof(NavigationController).GetMethod(name)
            ?? throw new Xunit.Sdk.XunitException($"NavigationController.{name} not found.");

    private static string? PermissionOf(MethodInfo action) =>
        action.GetCustomAttributes<HasPermissionAttribute>().SingleOrDefault()?.Permission;

    [Fact]
    public void GetMenu_stays_open_no_permission_gate()
    {
        // Every tenant_user may fetch its OWN menu; gating this would break the sidebar for non-admins.
        Assert.Null(PermissionOf(Action(nameof(NavigationController.GetMenu))));
    }

    [Fact]
    public void GetPreferences_is_gated_behind_manage()
    {
        Assert.Equal(ManageKey, PermissionOf(Action(nameof(NavigationController.GetPreferences))));
    }

    [Fact]
    public void ReplacePreferences_is_gated_behind_manage()
    {
        Assert.Equal(ManageKey, PermissionOf(Action(nameof(NavigationController.ReplacePreferences))));
    }

    [Fact]
    public void Gate_is_per_action_not_class_level()
    {
        // A class-level gate would (incorrectly) close GET menu too — the fix must be action-scoped.
        Assert.Empty(typeof(NavigationController).GetCustomAttributes<HasPermissionAttribute>());
    }
}
