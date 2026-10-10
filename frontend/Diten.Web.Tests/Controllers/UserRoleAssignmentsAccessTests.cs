using System.Net;
using System.Security.Claims;
using Diten.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-URA-FIX-01 (UAS-001) — the User Roles screen (/UserRoleAssignments) is opened by <c>auth.users.assign-role</c>,
/// the key the platform manifest's USER_ROLES page requires and AuthService's role assign / revoke demand. Without it:
/// the page answers 403 and the shell's access-denied surface (no heading, no picker, no role cards, no antiforgery
/// form, no script), and the two write proxies refuse with 403 before any call leaves for the gateway. Measured on the
/// real controller and on the real view + layout.
/// </summary>
public sealed class UserRoleAssignmentsAccessTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string View = "/Views/Governance/UserRoleAssignments/Index.cshtml";
    private const string Key = "auth.users.assign-role";
    private static readonly Guid UserId = Guid.Parse("4a8f8f3e-0000-4000-8000-000000000001");
    private static readonly Guid RoleId = Guid.Parse("4a8f8f3e-0000-4000-8000-000000000002");

    [Fact]
    public void The_page_without_the_key_answers_403_with_its_view_never_a_redirect()
    {
        var controller = Controller(new CountingGateway(), "auth.users.read");

        var result = controller.Index();

        Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
        Assert.Equal("~/Views/Governance/UserRoleAssignments/Index.cshtml", Assert.IsType<ViewResult>(result).ViewName);
    }

    [Fact]
    public void The_page_with_the_key_answers_200()
    {
        var controller = Controller(new CountingGateway(), Key);

        controller.Index();

        Assert.Equal(StatusCodes.Status200OK, controller.Response.StatusCode);
    }

    [Theory]
    [InlineData("assign")]
    [InlineData("revoke")]
    public async Task A_write_without_the_key_is_refused_with_403_and_never_reaches_the_gateway(string action)
    {
        var gateway = new CountingGateway();
        var controller = Controller(gateway, "auth.users.read");

        var result = action == "assign" ? await controller.Assign(UserId, RoleId) : await controller.Revoke(UserId, RoleId);

        var refused = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, refused.StatusCode);
        Assert.Equal(0, gateway.Calls);
    }

    [Theory]
    [InlineData("assign")]
    [InlineData("revoke")]
    public async Task A_write_with_the_key_reaches_the_gateway(string action)
    {
        var gateway = new CountingGateway();
        var controller = Controller(gateway, Key);

        var result = action == "assign" ? await controller.Assign(UserId, RoleId) : await controller.Revoke(UserId, RoleId);

        Assert.IsType<JsonResult>(result);
        Assert.Equal(1, gateway.Calls);
    }

    [Fact]
    public async Task With_the_layout_the_unauthorized_face_is_the_access_denied_surface_and_nothing_of_the_screen()
    {
        var denied = await LayoutRenderer.RenderAsync(factory, View, "UserRoleAssignments", "auth.users.read");

        Assert.Contains("diten-access-denied", denied);
        Assert.DoesNotContain("uraUserSelect", denied);
        Assert.DoesNotContain("uraAntiForgery", denied);
        Assert.DoesNotContain("Governance/UserRoleAssignments/index.js", denied);
        Assert.DoesNotContain("skeleton-loader", denied);
    }

    [Fact]
    public async Task With_the_layout_the_holder_of_the_key_gets_the_screen()
    {
        var reader = await LayoutRenderer.RenderAsync(factory, View, "UserRoleAssignments", Key);

        Assert.Contains("uraUserSelect", reader);
        Assert.Contains("uraAntiForgery", reader);
        Assert.Contains("Governance/UserRoleAssignments/index.js", reader);
        Assert.DoesNotContain("diten-access-denied", reader);
    }

    private static UserRoleAssignmentsController Controller(HttpMessageHandler gateway, string permission)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" })
            .Build();
        var controller = new UserRoleAssignmentsController(new HttpClient(gateway), configuration, new KeyLocalizer(),
            NullLogger<UserRoleAssignmentsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("tenantId", Guid.NewGuid().ToString("D")),
                        new Claim("permission", permission)
                    ], "test"))
                }
            }
        };
        return controller;
    }

    private sealed class CountingGateway : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
