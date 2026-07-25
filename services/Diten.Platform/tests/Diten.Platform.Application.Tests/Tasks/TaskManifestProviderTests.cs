using System.Reflection;
using System.Text.RegularExpressions;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

// MOD-0024 — the manifest is load-bearing twice over: it authors the permission keys' Module/Scope attribution
// (otherwise the reflection worker stamps them PlatformAdmin and they can never be granted to a tenant role), and
// it is the only place notification events are declared. These tests assert both, including the conditions the
// catalog requires before an event can reach Active.
public sealed class TaskManifestProviderTests
{
    private static readonly ModuleManifestDocument Manifest = new TaskManifestProvider().GetManifest();

    private static readonly HashSet<string> KnownPermissionKeys = typeof(TaskPermissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Declares_a_clean_slug_entitlement_gated_module_identity()
    {
        // Module="tasks" is what the permission keys get attributed to.
        Assert.Equal("tasks", Manifest.ModuleCode);
        Assert.Equal("DitenPlatform", Manifest.Service);
        Assert.True(Manifest.IsTenantAssignable);
        Assert.False(Manifest.IsBaseline);
        Assert.NotEmpty(Manifest.Pages);
    }

    [Fact]
    public void Every_declared_permission_is_a_real_TaskPermissions_constant()
    {
        Assert.NotEmpty(KnownPermissionKeys);

        foreach (var page in Manifest.Pages)
        {
            Assert.Contains(page.RequiredPermission, KnownPermissionKeys);
        }

        foreach (var action in Manifest.Pages.SelectMany(p => p.Actions))
        {
            Assert.Contains(action.PermissionKey, KnownPermissionKeys);
        }
    }

    [Fact]
    public void Every_permission_constant_has_a_manifest_home_so_none_is_left_to_the_reflection_worker()
    {
        // A key the manifest never mentions would be created by the A1 worker as Module="platform" +
        // Scope=PlatformAdmin — a scope AuthService cannot downgrade, permanently unassignable to a tenant role.
        var declared = Manifest.Pages.Select(p => p.RequiredPermission)
            .Concat(Manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey))
            .ToHashSet(StringComparer.Ordinal);

        var orphans = KnownPermissionKeys.Except(declared).ToList();
        Assert.True(orphans.Count == 0, $"Permission keys with no manifest home: {string.Join(", ", orphans)}");
    }

    [Fact]
    public void No_page_route_is_under_Platform_so_the_derived_scope_is_Tenant()
    {
        // ScopeFromRoute yields PlatformAdmin for "/Platform/..." — these are tenant surfaces and must not be.
        Assert.All(Manifest.Pages, p =>
            Assert.False(p.RoutePath.StartsWith("/Platform", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Pages_are_nav_invisible_so_they_do_not_compete_with_the_Task_Center()
    {
        // Görev Merkezi is the single personal entry point; a second "Görevler" sidebar item would fragment it.
        Assert.All(Manifest.Pages, p => Assert.False(p.IsNavigationVisible));
    }

    [Fact]
    public void Page_codes_and_routes_are_unique_so_the_reconcile_cannot_skip_one()
    {
        var codes = Manifest.Pages.Select(p => p.PageCode).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var routes = Manifest.Pages.Select(p => p.RoutePath).ToList();
        Assert.Equal(routes.Count, routes.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var actionCodes = Manifest.Pages.SelectMany(p => p.Actions.Select(a => $"{p.PageCode}:{a.ActionCode}")).ToList();
        Assert.Equal(actionCodes.Count, actionCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void The_legacy_api_tasks_route_is_never_reused()
    {
        // frontend TaskApiController owns api/tasks and is frozen.
        Assert.All(Manifest.Pages, p =>
            Assert.False(p.RoutePath.StartsWith("/api/tasks", StringComparison.OrdinalIgnoreCase)));
    }

    // ── Notification events ─────────────────────────────────────────────────

    [Fact]
    public void Notification_events_are_declared_and_email_only()
    {
        var events = Manifest.NotificationEvents;
        Assert.NotNull(events);
        Assert.NotEmpty(events!);

        // There is no in-app channel in the platform (NotificationChannelCode { Email = 0 }); the bell is BL-025.
        Assert.All(events!, e => Assert.Equal("Email", e.Channel));
    }

    [Fact]
    public void Every_event_can_actually_reach_Active()
    {
        // The catalog only activates an event when it declares Status "Active" AND has zero validation issues.
        var events = Manifest.NotificationEvents!;
        Assert.All(events, e => Assert.Equal("Active", e.Status));

        var dottedKey = new Regex(@"^[a-z0-9]+(\.[a-z0-9]+)*$");
        var variableName = new Regex(@"^[A-Za-z][A-Za-z0-9_.]*$");
        string[] catalogVariableTypes = ["String", "Number", "Boolean", "Date", "Url"];

        var pageCodes = Manifest.Pages.Select(p => p.PageCode).ToHashSet(StringComparer.Ordinal);
        var permissionKeys = Manifest.Pages.Select(p => p.RequiredPermission)
            .Concat(Manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var e in events)
        {
            Assert.Matches(dottedKey, e.EventCode);
            Assert.Matches(dottedKey, e.DefaultTemplateKey);

            // TargetPageCode / RequiredPermissionKey are validated against THIS manifest's own sets.
            Assert.NotNull(e.TargetPageCode);
            Assert.Contains(e.TargetPageCode!, pageCodes);
            Assert.NotNull(e.RequiredPermissionKey);
            Assert.Contains(e.RequiredPermissionKey!, permissionKeys);

            foreach (var v in (e.RequiredVariables ?? []).Concat(e.OptionalVariables ?? []))
            {
                Assert.Matches(variableName, v.Name);
                Assert.Contains(v.Type, catalogVariableTypes);
            }
        }
    }

    [Fact]
    public void Event_codes_match_the_constants_the_handlers_dispatch()
    {
        var declared = Manifest.NotificationEvents!.Select(e => e.EventCode).ToHashSet(StringComparer.Ordinal);

        // A handler dispatching a code the manifest never declared would silently never send.
        Assert.Contains(TaskNotificationEvents.Assigned, declared);
        Assert.Contains(TaskNotificationEvents.Claimed, declared);
        Assert.Contains(TaskNotificationEvents.DueSoon, declared);
        Assert.Contains(TaskNotificationEvents.Completed, declared);
        Assert.Contains(TaskNotificationEvents.ApprovalRequested, declared);
    }

    [Fact]
    public void Event_codes_are_unique()
    {
        var codes = Manifest.NotificationEvents!.Select(e => e.EventCode).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
    }
}
