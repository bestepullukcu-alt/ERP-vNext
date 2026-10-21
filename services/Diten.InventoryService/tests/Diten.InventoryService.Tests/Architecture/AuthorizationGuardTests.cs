using System.Reflection;
using Diten.InventoryService.Api.Controllers.Quarantine;
using Diten.InventoryService.Api.Controllers.Traceability;
using Diten.InventoryService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.InventoryService.Tests.Architecture;

/// <summary>
/// WP-MVP1-FIX-LIVE3 (AGENTS.md core rule 3: every action carries [HasPermission]) — the service-wide guard. Every HTTP action of every
/// Inventory controller carries its key ON THE ACTION OR ITS CONTROLLER — <see cref="HasPermissionAttribute"/> or
/// <see cref="HasAnyPermissionAttribute"/> — where the manifest / permission-catalogue guards can read it; an action without one is a named
/// exception with its reason (<see cref="NamedExceptions"/>, the Platform pattern). The actions whose key ALSO depends on what the body says
/// (which of the accepted keys applies) are named in <see cref="BodyKeyedActions"/>: the attribute still lists every key they accept.
/// </summary>
public sealed class AuthorizationGuardTests
{
    /// <summary>"Controller.Action" → why no permission attribute carries its key.</summary>
    internal static readonly IReadOnlyDictionary<string, string> NamedExceptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["QuarantineESignSubjectsController.GetSignableSubject"] = "ESIGN-BUNDLE §6 subject provider (MOD-0022, the signer's token): every refusal — a missing quarantine.holds.read included — is ONE 404, no oracle; an attribute would answer 403. [Authorize] on the controller, the key checked in the action",
        ["ExpiryESignSubjectsController.GetSignableSubject"] = "ESIGN-BUNDLE §6 subject provider (MOD-0022, the signer's token): every refusal — a missing expiry key included — is ONE 404, no oracle; an attribute would answer 403. [Authorize] on the controller, the key checked in the action",
        ["RecallESignSubjectsController.GetSignableSubject"] = "ESIGN-BUNDLE §6 subject provider (MOD-0022, the signer's token): every refusal — a missing recall.cases.read / recall.drills.read included — is ONE 404, no oracle; an attribute would answer 403. [Authorize] on the controller, the key checked in the action"
    };

    /// <summary>"Controller.Action" → why the attribute cannot say WHICH accepted key applies (it lists them all).</summary>
    internal static readonly IReadOnlyDictionary<string, string> BodyKeyedActions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["QuarantineController.CreateDecision"] = "pack §4.6: decisions.create / blocked.dispose / blocked.unblock follows the body's decision kind",
        ["TraceController.UpdateLot"] = "MOD-0174: lots.update (Draft) / lots.update-released (Released) follows the stored lot's status, read with the lot"
    };

    private static IEnumerable<(Type Controller, MethodInfo Action)> Actions()
        => typeof(QuarantineController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
                .Select(m => (t, m)));

    private static bool Keyed(MemberInfo member)
        => member.GetCustomAttributes<HasPermissionAttribute>(inherit: true).Any() || member.GetCustomAttributes<HasAnyPermissionAttribute>(inherit: true).Any();

    private static string Name((Type Controller, MethodInfo Action) a) => $"{a.Controller.Name}.{a.Action.Name}";

    [Fact]
    public void Every_action_carries_its_key_in_an_attribute_or_is_a_named_exception()
    {
        var actions = Actions().ToList();
        Assert.True(actions.Count > 100, $"the scan reached only {actions.Count} actions");
        var unkeyed = actions.Where(a => !Keyed(a.Action) && !Keyed(a.Controller)
                                          && a.Action.GetCustomAttribute<AllowAnonymousAttribute>() is null && a.Controller.GetCustomAttribute<AllowAnonymousAttribute>() is null)
            .Select(Name).Where(n => !NamedExceptions.ContainsKey(n)).ToList();
        Assert.True(unkeyed.Count == 0, "actions without a permission attribute: " + string.Join(", ", unkeyed));
        Assert.All(NamedExceptions.Keys, n => Assert.Contains(n, actions.Select(Name)));                     // the list only names real actions
    }

    [Fact]
    public void The_body_keyed_actions_list_every_key_they_accept()
    {
        var create = typeof(QuarantineController).GetMethod(nameof(QuarantineController.CreateDecision))!.GetCustomAttribute<HasAnyPermissionAttribute>()!;
        Assert.Equal([QuarantinePermissions.DecisionsCreate, QuarantinePermissions.BlockedDispose, QuarantinePermissions.BlockedUnblock], create.Permissions);
        var submit = typeof(QuarantineController).GetMethod(nameof(QuarantineController.SubmitDecision))!.GetCustomAttribute<HasAnyPermissionAttribute>()!;
        Assert.Equal(create.Permissions, submit.Permissions);
        var update = typeof(TraceController).GetMethod(nameof(TraceController.UpdateLot))!.GetCustomAttribute<HasAnyPermissionAttribute>()!;
        Assert.Equal([TracePermissions.LotsUpdate, TracePermissions.LotsUpdateReleased], update.Permissions);
        Assert.All(BodyKeyedActions.Keys, n => Assert.Contains(n, Actions().Select(Name)));
    }

    [Fact]
    public void An_any_permission_policy_is_built_from_its_keys()
    {
        var attribute = new HasAnyPermissionAttribute("a.b.c", "a.b.d");
        Assert.Equal("AnyPermission:a.b.c|a.b.d", attribute.Policy);
        Assert.Throws<ArgumentException>(() => new HasAnyPermissionAttribute());
        Assert.Throws<ArgumentException>(() => new HasAnyPermissionAttribute("a|b"));
    }
}
