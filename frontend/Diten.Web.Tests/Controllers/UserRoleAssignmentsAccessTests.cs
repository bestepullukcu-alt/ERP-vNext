using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
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
/// WP-URA-FIX-01 (+FIX1, UAS-001) — the two screens of the access-governance page family that hand out authority:
///   · User Roles (/UserRoleAssignments), opened by <c>auth.users.assign-role</c>;
///   · Role Permissions (/RoleAssignments), opened by <c>auth.roles.assign-permission</c> (FIX1, CT decision: same family).
/// Without the key: 403 and the shell's access-denied face (no heading, no picker, no antiforgery form, no script), and
/// the two write proxies refuse with 403 — the sentence a gateway 403 produces (`AccessDenied`) — before any call leaves
/// for the gateway. The controller and the view read the key through the SAME reader (PermissionClaims), so "*", a plural
/// "permissions" claim and a comma-separated list open the page on both sides at once. Measured on the real controllers
/// and on the real view + layout.
/// </summary>
public abstract class AccessGovernanceScreenAccessTests(WebApplicationFactory<Program> factory)
{
    protected abstract string View { get; }
    protected abstract string ControllerName { get; }
    protected abstract string Key { get; }
    protected abstract string ScreenMarker { get; }      // drawn for a holder of the key, never on the denied face
    protected abstract string AntiForgeryFormId { get; }
    protected abstract string Script { get; }
    protected abstract Controller CreateController(HttpMessageHandler gateway, params Claim[] claims);
    protected abstract Task<IActionResult> AssignAsync(Controller controller);
    protected abstract Task<IActionResult> RevokeAsync(Controller controller);
    protected abstract IActionResult Index(Controller controller);

    private static readonly Claim Tenant = new("tenantId", Guid.NewGuid().ToString("D"));

    [Fact]
    public void The_page_without_the_key_answers_403_with_its_view_never_a_redirect()
    {
        var controller = CreateController(new CountingGateway(), Tenant, new Claim("permission", "auth.users.read"));

        var result = Index(controller);

        Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
        Assert.Equal(View, "/" + Assert.IsType<ViewResult>(result).ViewName!.TrimStart('~', '/'));
    }

    [Fact]
    public void The_page_with_the_key_answers_200()
    {
        var controller = CreateController(new CountingGateway(), Tenant, new Claim("permission", Key));

        Index(controller);

        Assert.Equal(StatusCodes.Status200OK, controller.Response.StatusCode);
    }

    [Theory]
    [InlineData("assign")]
    [InlineData("revoke")]
    public async Task A_write_without_the_key_is_refused_with_403_and_never_reaches_the_gateway(string action)
    {
        var gateway = new CountingGateway();
        var controller = CreateController(gateway, Tenant, new Claim("permission", "auth.users.read"));

        var result = action == "assign" ? await AssignAsync(controller) : await RevokeAsync(controller);

        var refused = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, refused.StatusCode);
        Assert.Equal(0, gateway.Calls);
        // FIX1 — the body says WHAT: the gateway-403 sentence (the test localizer echoes the key it was asked for).
        var body = JsonSerializer.Serialize(refused.Value);
        Assert.Contains("AccessDenied", body);
        Assert.DoesNotContain("AccessDeniedMessage", body);
    }

    [Theory]
    [InlineData("assign")]
    [InlineData("revoke")]
    public async Task A_write_with_the_key_reaches_the_gateway(string action)
    {
        var gateway = new CountingGateway();
        var controller = CreateController(gateway, Tenant, new Claim("permission", Key));

        var result = action == "assign" ? await AssignAsync(controller) : await RevokeAsync(controller);

        Assert.IsType<JsonResult>(result);
        Assert.Equal(1, gateway.Calls);
    }

    [Theory]
    [InlineData("Assign")]
    [InlineData("Revoke")]
    public void Both_writes_demand_the_antiforgery_token(string method)
    {
        // The tests above call the actions directly, where MVC filters do not run; the attribute itself is measured.
        var action = CreateController(new CountingGateway(), Tenant).GetType().GetMethod(method)!;
        Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.NotNull(action.GetCustomAttribute<HttpPostAttribute>());
    }

    [Fact]
    public async Task With_the_layout_the_unauthorized_face_is_the_access_denied_surface_and_nothing_of_the_screen()
    {
        var denied = await LayoutRenderer.RenderAsync(factory, View, ControllerName, "auth.users.read");

        Assert.Contains("diten-access-denied", denied);
        Assert.DoesNotContain(ScreenMarker, denied);
        Assert.DoesNotContain(AntiForgeryFormId, denied);
        Assert.DoesNotContain(Script, denied);
    }

    [Fact]
    public async Task With_the_layout_the_holder_of_the_key_gets_the_screen()
    {
        var reader = await LayoutRenderer.RenderAsync(factory, View, ControllerName, Key);

        Assert.Contains(ScreenMarker, reader);
        Assert.Contains(AntiForgeryFormId, reader);
        Assert.Contains(Script, reader);
        Assert.DoesNotContain("diten-access-denied", reader);
    }

    [Fact]
    public async Task The_access_denied_face_names_the_screen()
    {
        var denied = await LayoutRenderer.RenderAsync(factory, View, ControllerName, "auth.users.read");

        // _AccessDenied's title is "You don't have access to {0}" — {0} is the screen's own title, never empty.
        Assert.Matches(@"<h5>You don(&#x27;|')t have access to [^<]+</h5>", denied);
    }

    public static TheoryData<string, string> ClaimShapes(string key) => new()
    {
        { "permission", "*" },
        { "permissions", $"auth.users.read,{key}" },
        { "permission", $"auth.users.read, {key}" }
    };

    protected async Task The_controller_and_the_view_read_the_same_claim_shape(string type, string value)
    {
        var claim = new Claim(type, value);
        var controller = CreateController(new CountingGateway(), Tenant, claim);
        Index(controller);
        var page = await LayoutRenderer.RenderWithClaimsAsync(factory, View, ControllerName, [claim]);

        Assert.Equal(StatusCodes.Status200OK, controller.Response.StatusCode);
        Assert.Contains(ScreenMarker, page);
        Assert.DoesNotContain("diten-access-denied", page);
    }

    protected static (IConfiguration Configuration, KeyLocalizer Localizer) Dependencies() =>
        (new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build(), new KeyLocalizer());

    protected static ControllerContext Context(params Claim[] claims) => new()
    {
        HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
    };

    protected sealed class CountingGateway : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }

    protected sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}

public sealed class UserRoleAssignmentsAccessTests(WebApplicationFactory<Program> factory)
    : AccessGovernanceScreenAccessTests(factory), IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid UserId = Guid.Parse("4a8f8f3e-0000-4000-8000-000000000001");
    private static readonly Guid RoleId = Guid.Parse("4a8f8f3e-0000-4000-8000-000000000002");

    protected override string View => "/Views/Governance/UserRoleAssignments/Index.cshtml";
    protected override string ControllerName => "UserRoleAssignments";
    protected override string Key => UserRoleAssignmentsController.AssignRolePermission;
    protected override string ScreenMarker => "id=\"uraSearch\"";
    protected override string AntiForgeryFormId => "uraAntiForgery";
    protected override string Script => "Governance/UserRoleAssignments/index.js";

    protected override Controller CreateController(HttpMessageHandler gateway, params Claim[] claims)
    {
        var (configuration, localizer) = Dependencies();
        return new UserRoleAssignmentsController(new HttpClient(gateway), configuration, localizer,
            NullLogger<UserRoleAssignmentsController>.Instance) { ControllerContext = Context(claims) };
    }

    protected override Task<IActionResult> AssignAsync(Controller controller) =>
        ((UserRoleAssignmentsController)controller).Assign(UserId, RoleId);

    protected override Task<IActionResult> RevokeAsync(Controller controller) =>
        ((UserRoleAssignmentsController)controller).Revoke(UserId, RoleId);

    protected override IActionResult Index(Controller controller) => ((UserRoleAssignmentsController)controller).Index();

    public static TheoryData<string, string> Shapes => ClaimShapes(UserRoleAssignmentsController.AssignRolePermission);

    [Theory]
    [MemberData(nameof(Shapes))]
    public Task The_controller_and_the_view_open_the_page_for_the_same_claim_shapes(string type, string value) =>
        The_controller_and_the_view_read_the_same_claim_shape(type, value);
}

public sealed class RoleAssignmentsAccessTests(WebApplicationFactory<Program> factory)
    : AccessGovernanceScreenAccessTests(factory), IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid RoleId = Guid.Parse("4a8f8f3e-0000-4000-8000-000000000003");
    private static readonly Guid PermissionId = Guid.Parse("4a8f8f3e-0000-4000-8000-000000000004");

    protected override string View => "/Views/Governance/RoleAssignments/Index.cshtml";
    protected override string ControllerName => "RoleAssignments";
    protected override string Key => RoleAssignmentsController.AssignPermissionPermission;
    protected override string ScreenMarker => "id=\"raRoleSelect\"";
    protected override string AntiForgeryFormId => "raAntiForgery";
    protected override string Script => "Governance/RoleAssignments/index.js";

    protected override Controller CreateController(HttpMessageHandler gateway, params Claim[] claims)
    {
        var (configuration, localizer) = Dependencies();
        return new RoleAssignmentsController(new HttpClient(gateway), configuration, localizer,
            NullLogger<RoleAssignmentsController>.Instance) { ControllerContext = Context(claims) };
    }

    protected override Task<IActionResult> AssignAsync(Controller controller) =>
        ((RoleAssignmentsController)controller).Assign(RoleId, PermissionId);

    protected override Task<IActionResult> RevokeAsync(Controller controller) =>
        ((RoleAssignmentsController)controller).Revoke(RoleId, PermissionId);

    protected override IActionResult Index(Controller controller) => ((RoleAssignmentsController)controller).Index();

    public static TheoryData<string, string> Shapes => ClaimShapes(RoleAssignmentsController.AssignPermissionPermission);

    [Theory]
    [MemberData(nameof(Shapes))]
    public Task The_controller_and_the_view_open_the_page_for_the_same_claim_shapes(string type, string value) =>
        The_controller_and_the_view_read_the_same_claim_shape(type, value);
}
