using System.Reflection;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.TimeEntry.SelfRegistration;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Domain.Entities.Workflow;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 §14 / §19.3 — the manifest carries exactly the six T1a keys, each a real constant, on tenant-scoped
/// routes; the module is a catalog module behind an entitlement; the T4 keys are not minted yet. Plus R5's parser.
/// </summary>
public sealed class TimeEntryManifestProviderTests
{
    private static readonly IReadOnlySet<string> RealKeys = typeof(TimeEntryPermissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet();

    private readonly Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument _manifest =
        new TimeEntryManifestProvider().GetManifest();

    [Fact]
    public void The_module_is_time_entry_a_catalog_module_behind_an_entitlement_in_Human_Capital()
    {
        Assert.Equal("time-entry", _manifest.ModuleCode);
        Assert.False(_manifest.IsBaseline);
        Assert.True(_manifest.IsTenantAssignable);
        Assert.Equal("Human Capital", _manifest.Domain);
    }

    [Fact]
    public void Every_declared_key_is_a_real_constant_and_all_six_T1a_keys_are_declared()
    {
        var declared = _manifest.Pages
            .Select(p => p.RequiredPermission)
            .Concat(_manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey))
            .ToHashSet();

        Assert.All(declared, key => Assert.Contains(key, RealKeys));
        Assert.Equal(RealKeys.OrderBy(k => k), declared.OrderBy(k => k));
        Assert.Equal(6, RealKeys.Count);
        Assert.All(RealKeys, key => Assert.StartsWith("time-entry.", key, StringComparison.Ordinal));
        Assert.All(RealKeys, key => Assert.Equal(3, key.Split('.').Length));
    }

    [Fact]
    public void The_T4_keys_are_not_minted_in_T1a()
    {
        Assert.DoesNotContain("time-entry.team-totals.read", RealKeys);
        Assert.DoesNotContain("time-entry.person-reports.read", RealKeys);
    }

    [Fact]
    public void Every_route_is_tenant_scoped_and_pages_are_unique_and_each_nav_page_has_its_own_key()
    {
        Assert.All(_manifest.Pages, p => Assert.StartsWith("/TimeEntry", p.RoutePath, StringComparison.Ordinal));
        Assert.DoesNotContain(_manifest.Pages, p => p.RoutePath.StartsWith("/Platform", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(_manifest.Pages.Count, _manifest.Pages.Select(p => p.PageCode).Distinct().Count());
        Assert.Equal(_manifest.Pages.Count, _manifest.Pages.Select(p => p.RoutePath).Distinct().Count());
        // T2a — only the screen that exists is in the nav; the rest wait for T2b.
        // T2b — every screen that exists is in the nav, each with its own key; the read-only detail is reached from its list.
        Assert.Equal(
            [TimeEntryManifestProvider.PageMyTimesheet, TimeEntryManifestProvider.PageApprovals,
             TimeEntryManifestProvider.PageCategories, TimeEntryManifestProvider.PageSettings],
            _manifest.Pages.Where(p => p.IsNavigationVisible).Select(p => p.PageCode).ToList());
        Assert.False(_manifest.Pages.Single(p => p.PageCode == TimeEntryManifestProvider.PageApprovalDetail).IsNavigationVisible);
        Assert.All(_manifest.Pages.Where(p => p.IsNavigationVisible), p => Assert.StartsWith("time-entry.", p.RequiredPermission));
        Assert.All(_manifest.Pages, p => Assert.Equal(p.Actions.Count, p.Actions.Select(a => a.ActionCode).Distinct().Count()));
    }

    [Fact]
    public void The_notification_events_are_declared_on_real_pages_and_keys_as_Active()
    {
        var events = _manifest.NotificationEvents!;
        var pages = _manifest.Pages.Select(p => p.PageCode).ToHashSet();

        // T3 (N7) — the five T1a events plus the reminder and the minutes conflict, all Active.
        Assert.Equal(TimeEntryNotificationEvents.All.OrderBy(c => c), events.Select(e => e.EventCode).OrderBy(c => c));
        Assert.All(events, e => Assert.Contains(e.TargetPageCode!, pages));
        Assert.All(events, e => Assert.Contains(e.RequiredPermissionKey!, RealKeys));
        Assert.All(events, e => Assert.Equal("Active", e.Status));
        Assert.All(events, e => Assert.Equal(e.EventCode, e.DefaultTemplateKey));
    }

    // ── R5 — the per-definition option, parsed ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("""{ "options": { "rejectCommentRequired": true } }""", true)]
    [InlineData("""{ "options": { "rejectCommentRequired": "true" } }""", true)]
    [InlineData("""{ "options": { "rejectCommentRequired": false } }""", false)]
    [InlineData("""{ "options": [] }""", false)]
    [InlineData("""{ "stages": [] }""", false)]
    [InlineData("""{ "name": "Task approval", "steps": [ { "requirements": { "commentRequired": true } } ] }""", false)]
    [InlineData("not json", false)]
    [InlineData("", false)]
    public void The_reject_comment_option_is_read_from_the_definition_and_defaults_to_off(string json, bool expected)
    {
        Assert.Equal(expected, WorkflowDefinitionRuntimePlan.RejectCommentRequired(Version(json)));
    }

    [Fact]
    public void The_timesheet_definition_turns_the_option_on()
    {
        Assert.True(WorkflowDefinitionRuntimePlan.RejectCommentRequired(Version(TimesheetApprovalService.DefinitionJson)));
        Assert.False(WorkflowDefinitionRuntimePlan.RejectCommentRequired(null));
    }

    private static WorkflowTemplateVersion Version(string json) => new()
    {
        TenantId = Guid.NewGuid(),
        TemplateId = Guid.NewGuid(),
        VersionNumber = 1,
        DefinitionJson = json,
        SchemaVersion = "1.0",
        ExpressionVersion = "1.0"
    };

    [Fact]
    public void T1b_capture_actions_ride_on_my_timesheet_with_no_new_key()
    {
        var page = _manifest.Pages.Single(p => p.PageCode == TimeEntryManifestProvider.PageMyTimesheet);
        var actions = page.Actions.ToDictionary(a => a.ActionCode, a => a.PermissionKey);

        Assert.Equal(TimeEntryPermissions.TimesheetsUpdate, actions["START_TIMER"]);
        Assert.Equal(TimeEntryPermissions.TimesheetsUpdate, actions["STOP_TIMER"]);
        Assert.Equal(TimeEntryPermissions.TimesheetsUpdate, actions["UNDO_TIMER_SWITCH"]);
        Assert.Equal(TimeEntryPermissions.TimesheetsUpdate, actions["ACCEPT_SUGGESTION"]);
        Assert.Equal(TimeEntryPermissions.TimesheetsUpdate, actions["DISMISS_SUGGESTION"]);
        Assert.Equal(TimeEntryPermissions.TimesheetsRead, actions["FILL_FROM_PLAN"]);
        Assert.True(page.IsNavigationVisible); // T2a — the page exists
    }
}
