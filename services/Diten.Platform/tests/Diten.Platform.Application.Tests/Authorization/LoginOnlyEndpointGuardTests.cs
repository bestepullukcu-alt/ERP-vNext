using System.Reflection;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Controllers.Test;
using Diten.Platform.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

/// <summary>
/// DCP-004 "Decision amendment 2026-09-15" (BL-410) — LOGIN-ONLY ENDPOINTS ARE LISTED BY A NAMED ATTRIBUTE.
///
/// <para><b>What was wrong.</b> "No <c>[HasPermission]</c>" meant two different things in this assembly and nothing
/// told them apart: a decision ("my own notifications need no key") and an omission (a resource endpoint somebody
/// forgot to gate). Measured before the change: 16 actions carried neither a permission gate, a platform policy nor
/// an anonymous marker, and the reader could tell which were deliberate only from comments.</para>
///
/// <para><b>The rule.</b> Every action in the Platform API assembly is exactly one of: key-gated
/// (<see cref="HasPermissionAttribute"/> / <see cref="RequiresExplicitPermissionAttribute"/>), platform-policy gated
/// (<c>[Authorize(Policy = PlatformActor | PlatformAdminOnly)]</c>), an allow-listed anonymous <c>/api/internal</c>
/// route (API key checked in the controller), <see cref="LoginOnlyAttribute"/> with a reason, or one of the named
/// exceptions below with its own reason. The login-only list is PINNED, so adding one is a reviewed change.</para>
///
/// <para>Measured on the production assembly by reflection — not on a copy of the attribute list.</para>
/// </summary>
public sealed class LoginOnlyEndpointGuardTests
{
    private static readonly Assembly Api = typeof(WorkItemsController).Assembly;

    private static readonly HashSet<string> PlatformPolicies = new(StringComparer.Ordinal) { "PlatformActor", "PlatformAdminOnly" };

    /// <summary>The reviewed login-only list (Controller.Action). Changing it is changing who can call what.</summary>
    private static readonly HashSet<string> ExpectedLoginOnly = new(StringComparer.Ordinal)
    {
        "AccessExplainController.ExplainMyAccess",
        "MyNotificationsController.GetMine",
        "MyNotificationsController.MarkAllRead",
        "MyNotificationsController.MarkRead",
        "NavigationController.GetMenu",
        "SavedViewsController.Create",
        "SavedViewsController.Delete",
        "SavedViewsController.GetViews",
        "SavedViewsController.Update",
        "TenantReferenceDataController.GetPublishedValues",
        "TenantReferenceLookupsController.GetCountries",
        "TenantReferenceLookupsController.GetCurrencies",
        "WorkItemsController.GetMine",
        "WorkItemsController.GetTeamAvailability"
    };

    /// <summary>Actions that are neither key-gated by attribute nor login-only, each with the gate they DO have.</summary>
    private static readonly Dictionary<string, string> NamedExceptions = new(StringComparer.Ordinal)
    {
        ["WorkItemsController.DispatchAction"] =
            "The key depends on the action code: the dispatcher names it and the action checks it; a blank key is "
            + "refused (WorkItemActionDispatchTests, WorkItemsForEveryTenantUserHttpTests).",
        [$"{nameof(AuthorizationProbeController)}.ModuleHr"] =
            "Test-only probe for [RequiresModule]; answers 404 outside Development/Test.",
        [$"{nameof(AuthorizationProbeController)}.FeatureAdvancedReporting"] =
            "Test-only probe for [RequiresFeature]; answers 404 outside Development/Test.",
        [$"{nameof(AuthorizationProbeController)}.ModuleAndFeature"] =
            "Test-only probe for [RequiresModule]+[RequiresFeature]; answers 404 outside Development/Test."
    };

    /// <summary>Anonymous controllers. Each must sit under /api/internal (bypass path, API key checked inside).</summary>
    private static readonly HashSet<string> AnonymousInternalControllers = new(StringComparer.Ordinal)
    {
        "InternalAuditController",
        "InternalBusinessReferenceDataController",
        "InternalModuleRegistrationController",
        "InternalPlatformAdministratorsController",
        "InternalPpmEntitlementDecisionController",
        "InternalQuotasController",
        "InternalTenantAdminActivationController",
        "InternalTenantBrandingController",
        "InternalTenantEntitlementsController",
        "InternalTenantLoginSettingsController",
        "InternalTenantResolveController",
        "InternalTenantStatusController",
        "InternalVerifiedMarketReferenceDataController"
    };

    [Fact]
    public void Every_action_has_a_key_a_platform_policy_an_internal_anonymous_route_LoginOnly_or_a_named_exception()
    {
        var actions = Actions();
        // Non-vacuity: a renamed base type or namespace must not make this pass over nothing.
        Assert.True(actions.Count > 300, $"only {actions.Count} actions were found — discovery is broken");

        var uncovered = actions
            .Where(a => !IsKeyGated(a) && !IsPlatformPolicyGated(a) && !IsAnonymous(a)
                        && !a.IsDefined(typeof(LoginOnlyAttribute), false) && !NamedExceptions.ContainsKey(Name(a)))
            .Select(Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(uncovered.Count == 0,
            "These actions carry no permission key, no platform policy and no [LoginOnly] — gate them, or mark them "
            + "[LoginOnly(\"why\")] and add them to the reviewed list: " + string.Join(", ", uncovered));
    }

    [Fact]
    public void The_login_only_list_is_exactly_the_reviewed_list()
    {
        var actual = Actions()
            .Where(a => a.IsDefined(typeof(LoginOnlyAttribute), false))
            .Select(Name)
            .ToHashSet(StringComparer.Ordinal);

        var missing = ExpectedLoginOnly.Except(actual).OrderBy(n => n).ToList();
        var added = actual.Except(ExpectedLoginOnly).OrderBy(n => n).ToList();

        Assert.True(missing.Count == 0 && added.Count == 0,
            $"Login-only list drifted. Lost the attribute: [{string.Join(", ", missing)}]. "
            + $"New, not reviewed: [{string.Join(", ", added)}].");
    }

    [Fact]
    public void Every_login_only_action_says_why_is_authenticated_and_names_no_key()
    {
        foreach (var action in Actions().Where(a => a.IsDefined(typeof(LoginOnlyAttribute), false)))
        {
            var name = Name(action);
            var marker = action.GetCustomAttribute<LoginOnlyAttribute>()!;

            Assert.False(string.IsNullOrWhiteSpace(marker.Reason), $"{name}: [LoginOnly] needs a reason.");
            // Login-only is "signed in", never "anybody".
            Assert.True(Authorizes(action).Any(), $"{name}: [LoginOnly] without [Authorize] would be anonymous.");
            Assert.False(IsAnonymous(action), $"{name}: [LoginOnly] and [AllowAnonymous] contradict each other.");
            // A key and "no key needed" cannot both be the decision.
            Assert.False(IsKeyGated(action), $"{name}: carries a permission key and [LoginOnly] at once.");
        }
    }

    [Fact]
    public void Every_anonymous_action_is_an_allow_listed_internal_api_key_route()
    {
        foreach (var action in Actions().Where(IsAnonymous))
        {
            var controller = action.DeclaringType!;
            Assert.True(AnonymousInternalControllers.Contains(controller.Name),
                $"{Name(action)} is [AllowAnonymous] but its controller is not on the internal allow-list.");

            var route = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;
            Assert.True(route.StartsWith("api/internal", StringComparison.OrdinalIgnoreCase),
                $"{Name(action)} is anonymous outside /api/internal (route '{route}').");
        }
    }

    [Fact]
    public void Every_platform_policy_is_a_known_platform_actor_policy()
    {
        // The named exceptions carry their own gates ([RequiresModule]/[RequiresFeature] are [Authorize] subclasses
        // with "RequiresModule:*"/"RequiresFeature:*" policies), which is why they are named rather than counted here.
        foreach (var action in Actions().Where(a => !NamedExceptions.ContainsKey(Name(a))))
        {
            foreach (var policy in Authorizes(action).Select(a => a.Policy).Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                Assert.True(PlatformPolicies.Contains(policy!), $"{Name(action)} uses unknown policy '{policy}'.");
            }
        }
    }

    [Fact]
    public void The_named_exceptions_and_allow_lists_do_not_outlive_their_actions()
    {
        var names = Actions().Select(Name).ToHashSet(StringComparer.Ordinal);
        Assert.All(NamedExceptions.Keys, key => Assert.Contains(key, names));
        Assert.All(ExpectedLoginOnly, key => Assert.Contains(key, names));

        var controllers = Actions().Select(a => a.DeclaringType!.Name).ToHashSet(StringComparer.Ordinal);
        Assert.All(AnonymousInternalControllers, key => Assert.Contains(key, controllers));

        // The exceptions are exceptions, not hidden login-only endpoints: none of them may carry the marker.
        Assert.All(Actions().Where(a => NamedExceptions.ContainsKey(Name(a))),
            a => Assert.False(a.IsDefined(typeof(LoginOnlyAttribute), false), Name(a)));
    }

    // ── discovery ────────────────────────────────────────────────────────────────────────────────────────────

    private static List<MethodInfo> Actions()
        => Api.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>(true).Any() && !m.IsDefined(typeof(NonActionAttribute), true))
            .ToList();

    private static string Name(MethodInfo action) => $"{action.DeclaringType!.Name}.{action.Name}";

    private static bool IsKeyGated(MethodInfo action)
        => action.IsDefined(typeof(HasPermissionAttribute), true)
           || action.DeclaringType!.IsDefined(typeof(HasPermissionAttribute), true)
           || action.IsDefined(typeof(RequiresExplicitPermissionAttribute), true);

    private static bool IsPlatformPolicyGated(MethodInfo action)
        => Authorizes(action).Any(a => a.Policy is { } policy && PlatformPolicies.Contains(policy));

    private static bool IsAnonymous(MethodInfo action)
        => action.IsDefined(typeof(AllowAnonymousAttribute), true)
           || action.DeclaringType!.IsDefined(typeof(AllowAnonymousAttribute), true);

    private static IEnumerable<AuthorizeAttribute> Authorizes(MethodInfo action)
        => action.GetCustomAttributes<AuthorizeAttribute>(true)
            .Concat(action.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>(true));
}
