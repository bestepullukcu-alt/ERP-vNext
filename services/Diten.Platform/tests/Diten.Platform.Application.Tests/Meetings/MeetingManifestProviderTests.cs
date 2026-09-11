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
    public void Declares_exactly_nine_permission_keys_matching_MeetingPermissions()
    {
        Assert.Equal(9, KnownPermissionKeys.Count);

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

    [Fact]
    public void No_page_is_navigation_visible_this_slice_so_no_dead_menu_link_appears_before_S3()
    {
        Assert.All(Manifest.Pages, p => Assert.False(p.IsNavigationVisible));
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
}
