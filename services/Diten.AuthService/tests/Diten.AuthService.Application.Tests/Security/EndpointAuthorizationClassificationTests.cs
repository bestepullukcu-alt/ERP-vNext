using System.Reflection;
using Diten.AuthService.Api.Controllers;
using Diten.AuthService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Diten.AuthService.Application.Tests.Security;

// S5 / AG-STEP-006B — pins the per-endpoint authorization classification
// (docs/006b-endpoint-authorization-classification.md). Prevents drift in BOTH directions:
//  • a new admin-op endpoint cannot ship without [HasPermission];
//  • a self-service / internal-S2S endpoint cannot be accidentally [HasPermission]-guarded (lock-out).
public sealed class EndpointAuthorizationClassificationTests
{
    private static IEnumerable<MethodInfo> ActionMethods(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>().Any());

    private static bool HasPermissionGuard(MethodInfo m) =>
        m.GetCustomAttribute<HasPermissionAttribute>() is not null;

    private static bool IsExplicitlyAnonymous(MethodInfo m) =>
        m.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

    [Theory]
    [InlineData(typeof(UsersController))]
    [InlineData(typeof(RolesController))]
    [InlineData(typeof(PermissionsController))]
    public void Admin_op_controllers_guard_every_action_with_has_permission(Type controller)
    {
        var actions = ActionMethods(controller).ToList();
        Assert.NotEmpty(actions);

        // Every admin-op action must be [HasPermission]-guarded. An explicit [AllowAnonymous] is a
        // deliberate, reviewable class-(b) classification (e.g. invitation set-password, where the
        // user acts before holding any permission) — not the silent "forgot to guard" drift this pins.
        var unguarded = actions
            .Where(m => !HasPermissionGuard(m) && !IsExplicitlyAnonymous(m))
            .Select(m => m.Name)
            .ToList();
        Assert.True(unguarded.Count == 0, $"{controller.Name} has unguarded admin-op actions: {string.Join(", ", unguarded)}");
    }

    [Theory]
    // (b) self-service — guarding would lock the caller out of the flow that grants access.
    [InlineData(typeof(AuthController), nameof(AuthController.Logout))]
    [InlineData(typeof(AuthController), nameof(AuthController.ChangePassword))]
    [InlineData(typeof(AuthController), nameof(AuthController.Me))]
    [InlineData(typeof(PlatformAuthController), nameof(PlatformAuthController.ForcedChangePassword))]
    // (b) self-service — invited user redeems their set-password link before holding any permission.
    [InlineData(typeof(UsersController), nameof(UsersController.SetPassword))]
    // (c) internal service-to-service — no user JWT; [HasPermission] would break the caller.
    [InlineData(typeof(PlatformAuthController), nameof(PlatformAuthController.ProvisionPlatformAdmin))]
    [InlineData(typeof(PlatformAuthController), nameof(PlatformAuthController.SyncPlatformAdmin))]
    [InlineData(typeof(InternalEventsController), nameof(InternalEventsController.TenantActivated))]
    [InlineData(typeof(InternalEventsController), nameof(InternalEventsController.TenantAdminInvited))]
    public void Lifecycle_and_internal_endpoints_are_not_has_permission_guarded(Type controller, string methodName)
    {
        var method = controller.GetMethod(methodName)!;
        Assert.False(HasPermissionGuard(method),
            $"{controller.Name}.{methodName} must NOT carry [HasPermission] (self-service/internal — lock-out risk).");
    }
}
