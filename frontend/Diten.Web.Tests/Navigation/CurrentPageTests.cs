using Diten.Web.Services.Navigation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Xunit;

namespace Diten.Web.Tests.Navigation;

/// <summary>
/// WP-UX-COMMON-01 (BL-495) — the page code the help panel is keyed by: the menu's stable code for the page whose route
/// is the longest segment-boundary match, a view's explicit override, or the platform folder code.
/// </summary>
public sealed class CurrentPageTests
{
    private static readonly (string?, string?)[] Menu =
    [
        ("TASKS", "/Tasks"),
        ("TASKCREATE", "/Tasks/Create"),
        ("users", "/Governance/Users"),
        ("ROLE_PERMISSIONS", "/Governance/RolePermissions"),
        ("BOARD", "/Board?view=kanban#top"),
        ("BROKEN", null),
        ("EXTERNAL", "https://example.test/Tasks")
    ];

    [Theory]
    [InlineData("/Tasks", "TASKS")]
    [InlineData("/tasks/", "TASKS")]
    [InlineData("/Tasks/Create", "TASKCREATE")]
    [InlineData("/Tasks/0b7e/Edit", "TASKS")]
    [InlineData("/Governance/Users", "USERS")]
    [InlineData("/Governance/RolePermissions", "ROLEPERMISSIONS")]
    [InlineData("/Board", "BOARD")]        // U6 — a route carrying a query / fragment still matches its path
    public void The_longest_route_on_a_segment_boundary_wins_and_the_code_is_normalized(string path, string expected)
    {
        Assert.Equal(expected, CurrentPage.Match(path, Menu));
    }

    [Theory]
    [InlineData("/TasksArchive")]
    [InlineData("/Governance")]
    [InlineData("")]
    [InlineData(null)]
    public void A_path_no_page_owns_has_no_code(string? path)
    {
        Assert.Null(CurrentPage.Match(path, Menu));
    }

    [Theory]
    [InlineData("/Views/Platform/Tenants/Index.cshtml", "PLATFORMTENANTS")]
    [InlineData("/Views/Platform/ModuleCatalog/PageDetails.cshtml", "PLATFORMMODULECATALOG")]
    [InlineData("/Views/Governance/Users/Index.cshtml", null)]
    [InlineData(null, null)]
    public void A_platform_admin_page_is_coded_by_its_folder(string? viewPath, string? expected)
    {
        Assert.Equal(expected, CurrentPage.ForPlatformView(viewPath));
    }

    [Fact]
    public void A_view_override_wins_over_the_menu_match_and_is_normalized()
    {
        var context = new DefaultHttpContext();
        context.Items[CurrentPage.PageCodeItem] = "TASKS";
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());

        Assert.Equal("TASKS", CurrentPage.Resolve(context, viewData));

        viewData[CurrentPage.PageCodeViewData] = "time-approvals";
        Assert.Equal("TIMEAPPROVALS", CurrentPage.Resolve(context, viewData));
    }

    [Fact]
    public void The_access_denied_mark_lives_on_the_request()
    {
        var context = new DefaultHttpContext();
        Assert.False(CurrentPage.IsAccessDenied(context));
        CurrentPage.MarkAccessDenied(context);
        Assert.True(CurrentPage.IsAccessDenied(context));
        Assert.False(CurrentPage.IsAccessDenied(null));
    }
}
