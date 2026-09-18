using System.Net;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Navigation;

/// <summary>
/// DCP-004 "Decision amendment 2026-09-15" (BL-410) — a tenant user holding NO permission key sees Görev Merkezi in
/// Ctrl+K and in the sidebar.
///
/// <para>The Platform half (baseline module, page without a key → the menu row carries no RequiredPermission) is
/// measured in <c>WorkCenterNavigationForEveryTenantUserTests</c>. This file measures the Web half: what the two
/// per-user gates do with that row for a user whose permission snapshot is EMPTY. Ctrl+K is driven through the real
/// <see cref="TenantSearchController"/>; the sidebar gate lives in a Razor <c>@functions</c> block that cannot be
/// invoked without rendering, so its rule is pinned from the view source.</para>
/// </summary>
public sealed class WorkCenterMenuWithoutPermissionsTests
{
    private const string WorkCenterRoute = "/WorkCenterNext";

    [Fact]
    public async Task Ctrl_K_lists_Gorev_Merkezi_for_a_user_with_no_permissions_when_the_page_needs_no_key()
    {
        var json = await CtrlKFor(requiredPermission: null);

        Assert.Contains(WorkCenterRoute, json);
    }

    [Fact]
    public async Task Ctrl_K_hides_it_from_the_same_user_when_the_page_requires_a_key()
    {
        // Non-vacuity: the gate is live — a key on the page (the pre-amendment manifest) hides the entry.
        var json = await CtrlKFor(requiredPermission: "platform.work-aggregation.inbox.view");

        Assert.DoesNotContain(WorkCenterRoute, json);
    }

    [Fact]
    public void The_sidebar_shows_an_item_with_no_required_permission_to_every_user()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var view = File.ReadAllText(Path.Combine(
            repoRoot, "Diten.Web", "Views", "Shared", "Components", "DynamicModuleMenu", "Default.cshtml"));

        Assert.Contains("string.IsNullOrWhiteSpace(node.RequiredPermission) || Perms.Has(node.RequiredPermission!)", view);
    }

    private static async Task<string> CtrlKFor(string? requiredPermission)
    {
        var menu = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new
                {
                    moduleCode = "WORK-AGGREGATION",
                    moduleDisplayName = "Görev Merkezi / Task Center",
                    domain = "Workspace",
                    domainDisplayName = "Workspace",
                    icon = "bx-been-here",
                    items = new[]
                    {
                        new
                        {
                            pageCode = "WORKCENTER",
                            displayName = "Görev Merkezi",
                            routePath = WorkCenterRoute,
                            requiredPermission,
                            parentPageCode = (string?)null,
                            iconHint = (string?)null,
                            sortOrder = 10
                        }
                    }
                }
            }
        });

        var controller = new TenantSearchController(
            new HttpClient(new StubHandler(menu)) { BaseAddress = new Uri("http://localhost") },
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://localhost:5000" })
                .Build(),
            new NoPermissions(),
            new PassThroughNavLocalizer(),
            new EchoSharedLocalizer(),
            NullLogger<TenantSearchController>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{AuthTokenCookies.AccessTokenCookie}=A-TOKEN";
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        var result = Assert.IsType<JsonResult>(await controller.Data());
        return JsonSerializer.Serialize(result.Value);
    }

    /// <summary>A tenant user whose token carries no permission claim at all.</summary>
    private sealed class NoPermissions : Diten.Web.Services.IPermissionSnapshot
    {
        public bool Has(string permissionKey) => false;
        public IReadOnlyCollection<string> Keys => Array.Empty<string>();
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }

    private sealed class EchoSharedLocalizer : IStringLocalizer<Diten.Web.SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class PassThroughNavLocalizer : Diten.Web.Services.Navigation.INavNameLocalizer
    {
        public string Domain(string? domainCode, string serverName, bool isOverride) => serverName;
        public string Module(string? moduleCode, string serverName, bool isOverride) => serverName;
        public string Page(string? pageCode, string serverName) => serverName;
    }
}
