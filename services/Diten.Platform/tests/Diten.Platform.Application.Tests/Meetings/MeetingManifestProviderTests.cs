using System.Reflection;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

// MOD-0357 S2, AC10 — the manifest is what makes the nine platform.meetings.* keys attribute
// Module=meetings/Scope=Tenant instead of the A1 reflection worker's PlatformAdmin default (mirrors
// TaskManifestProviderTests' own reasoning for MOD-0024).
public sealed class MeetingManifestProviderTests
{
    private static readonly Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument Manifest =
        new MeetingManifestProvider().GetManifest();

    private static readonly HashSet<string> KnownPermissionKeys = typeof(MeetingPermissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Declares_the_clean_meetings_module_slug()
    {
        Assert.Equal("meetings", Manifest.ModuleCode);
        Assert.Equal("DitenPlatform", Manifest.Service);
        Assert.True(Manifest.IsTenantAssignable);
        Assert.NotEmpty(Manifest.Pages);
    }

    [Fact]
    public void Declares_exactly_ten_permission_keys_matching_MeetingPermissions()
    {
        // S11 — SeriesManage is the tenth key, added the same way TypesManage (S8) became the ninth.
        Assert.Equal(10, KnownPermissionKeys.Count);

        var declared = Manifest.Pages
            .Select(p => p.RequiredPermission)
            .Concat(Manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(KnownPermissionKeys, declared);
    }

    [Fact]
    public void Every_declared_permission_is_a_real_MeetingPermissions_constant()
    {
        foreach (var page in Manifest.Pages)
        {
            Assert.Contains(page.RequiredPermission, KnownPermissionKeys);
        }

        foreach (var action in Manifest.Pages.SelectMany(p => p.Actions))
        {
            Assert.Contains(action.PermissionKey, KnownPermissionKeys);
        }
    }

    /// <summary>S3/S8/S11/S12 — the top-level list, the meeting-type setting screen, the meeting-series setting
    /// screen AND the report screen are the module's nav entries; the three work-surface routes
    /// (Create/Detail/Edit) stay nav-hidden, reached only from the list, the same shape TaskManifestProvider's
    /// own TASK_CREATE/TASK_DETAIL/TASK_EDIT pages take (its own TASK_TYPES page is nav-visible too, under
    /// TASKS — MEETING_TYPES/MEETING_SERIES/MEETING_REPORT mirror it under MEETINGS).</summary>
    [Fact]
    public void Only_the_list_the_two_setting_screens_and_the_report_are_navigation_visible()
    {
        var visible = Manifest.Pages.Where(p => p.IsNavigationVisible).Select(p => p.PageCode).ToList();
        Assert.Equal(
            ["MEETINGS", "MEETING_REPORT", "MEETING_SERIES", "MEETING_TYPES"],
            visible.OrderBy(c => c, StringComparer.Ordinal));
    }

    /// <summary>S8 — the type setting screen is a child of MEETINGS, gated on TypesManage (not Read), and its
    /// own three actions (Create/Edit/Delete — unlike TASK_TYPES, MeetingType genuinely supports delete).</summary>
    [Fact]
    public void The_meeting_types_page_is_a_types_manage_gated_child_of_meetings_with_crud_actions()
    {
        var page = Manifest.Pages.Single(p => p.PageCode == "MEETING_TYPES");
        Assert.Equal("MEETINGS", page.ParentPageCode);
        Assert.Equal(MeetingPermissions.TypesManage, page.RequiredPermission);
        Assert.True(page.IsNavigationVisible);
        Assert.Equal(3, page.Actions.Count);
        Assert.All(page.Actions, a => Assert.Equal(MeetingPermissions.TypesManage, a.PermissionKey));
        Assert.Contains(page.Actions, a => a.ActionCode == "DELETE" && a.IsDangerous);
    }

    /// <summary>S11 — the series setting screen is a child of MEETINGS, gated on SeriesManage (not Read), and
    /// its own three actions (Create/Edit/Delete), the same shape MEETING_TYPES already takes.</summary>
    [Fact]
    public void The_meeting_series_page_is_a_series_manage_gated_child_of_meetings_with_crud_actions()
    {
        var page = Manifest.Pages.Single(p => p.PageCode == "MEETING_SERIES");
        Assert.Equal("MEETINGS", page.ParentPageCode);
        Assert.Equal(MeetingPermissions.SeriesManage, page.RequiredPermission);
        Assert.True(page.IsNavigationVisible);
        Assert.Equal(3, page.Actions.Count);
        Assert.All(page.Actions, a => Assert.Equal(MeetingPermissions.SeriesManage, a.PermissionKey));
        Assert.Contains(page.Actions, a => a.ActionCode == "DELETE" && a.IsDangerous);
    }

    [Fact]
    public void The_three_work_surface_routes_exist_nav_hidden_under_the_list()
    {
        var byCode = Manifest.Pages.ToDictionary(p => p.PageCode);
        foreach (var code in new[] { "MEETING_CREATE", "MEETING_DETAIL", "MEETING_EDIT" })
        {
            Assert.True(byCode.TryGetValue(code, out var page), $"Expected a {code} page.");
            Assert.False(page!.IsNavigationVisible);
            Assert.Equal("MEETINGS", page.ParentPageCode);
        }
    }

    [Fact]
    public void Every_permission_key_follows_the_lowercase_dotted_platform_meetings_convention()
    {
        foreach (var key in KnownPermissionKeys)
        {
            Assert.StartsWith("platform.meetings.", key);
            Assert.Equal(key, key.ToLowerInvariant());
        }
    }

    /// <summary>BL-387 — the three organizer-variant events declared alongside the invite/change/cancel siblings
    /// they mirror. <c>EventCode</c> == <c>DefaultTemplateKey</c> for every one of them, the same discipline the
    /// mailer's own doc comment relies on being kept in step by hand across manifest, mailer consts and seed.</summary>
    [Fact]
    public void Declares_the_three_organizer_variant_notification_events_with_matching_event_and_template_codes()
    {
        var expected = new[]
        {
            "platform.meetings.organizer-added",
            "platform.meetings.organizer-updated",
            "platform.meetings.organizer-cancelled"
        };

        foreach (var code in expected)
        {
            var ev = Assert.Single(Manifest.NotificationEvents!, e => e.EventCode == code);
            Assert.Equal(code, ev.DefaultTemplateKey);
            Assert.DoesNotContain(ev.RequiredVariables, v => v.Name == "Organizer");
            Assert.Contains(ev.RequiredVariables, v => v.Name == "MeetingUrl");
        }
    }
}
