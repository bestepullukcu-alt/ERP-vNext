using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web;
using Diten.Web.Controllers;
using Diten.Web.Models.Governance;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-ROLES-CLOSE-01 (A) — AuthService tags the role refusals with stable codes; the three governance proxies (Roles,
/// Role Permissions, User Roles) must hand the code to the screen on every door the screen uses, and must NOT hand
/// over AuthService's English sentence when there is no code. Driven on the REAL controllers with a gateway handler
/// answering exactly what AuthService answers.
/// </summary>
public sealed class GovernanceRoleRefusalProxyTests
{
    private const string Gateway = "http://gateway.test";
    private static readonly Guid RoleId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid OtherId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static string Refusal(string code, string message) =>
        $$$"""{"isSuccessful":false,"statusCode":409,"errors":["{{{message}}}"],"errorCodes":[{"code":"{{{code}}}"}]}""";

    [Fact]
    public async Task Roles_create_hands_over_ROLE_NAME_TAKEN()
    {
        var controller = Roles(new FixedGateway(HttpStatusCode.Conflict, Refusal("ROLE_NAME_TAKEN", "Role name is already in use.")));

        var json = Body(await controller.Create(new RoleEditViewModel { Name = "qa", DisplayName = "QA" }));

        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal("ROLE_NAME_TAKEN", json.GetProperty("errorCode").GetString());
        Assert.False(json.GetProperty("local").GetBoolean());
    }

    [Fact]
    public async Task Roles_edit_hands_over_ROLE_NOT_FOUND()
    {
        var controller = Roles(new FixedGateway(HttpStatusCode.NotFound, Refusal("ROLE_NOT_FOUND", "Role not found.")));

        var json = Body(await controller.Edit(RoleId, new RoleEditViewModel { Name = "qa", DisplayName = "QA" }));

        Assert.Equal("ROLE_NOT_FOUND", json.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Role_permission_assign_hands_over_the_code_even_on_a_403()
    {
        var controller = RoleAssignments(new FixedGateway(HttpStatusCode.Forbidden,
            Refusal("ROLE_PERMISSION_NOT_TENANT_ASSIGNABLE", "This permission cannot be assigned to a tenant role.")));

        var json = Body(await controller.Assign(RoleId, OtherId));

        Assert.Equal("ROLE_PERMISSION_NOT_TENANT_ASSIGNABLE", json.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Role_permission_revoke_hands_over_ROLE_PERMISSION_GRANT_MANAGED()
    {
        var controller = RoleAssignments(new FixedGateway(HttpStatusCode.Conflict,
            Refusal("ROLE_PERMISSION_GRANT_MANAGED", "System/Module grants are provisioning-managed and cannot be manually removed.")));

        var json = Body(await controller.Revoke(RoleId, OtherId));

        Assert.Equal("ROLE_PERMISSION_GRANT_MANAGED", json.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task User_role_assign_hands_over_USER_ROLE_USER_NOT_FOUND()
    {
        var controller = UserRoleAssignments(new FixedGateway(HttpStatusCode.NotFound, Refusal("USER_ROLE_USER_NOT_FOUND", "User not found.")));

        var json = Body(await controller.Assign(OtherId, RoleId));

        Assert.Equal("USER_ROLE_USER_NOT_FOUND", json.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task A_refusal_without_a_code_never_hands_over_the_gateway_sentence()
    {
        var controller = Roles(new FixedGateway(HttpStatusCode.BadRequest, """{"isSuccessful":false,"errors":["Some raw English sentence."]}"""));

        var json = Body(await controller.Create(new RoleEditViewModel { Name = "qa", DisplayName = "QA" }));

        Assert.Equal(JsonValueKind.Null, json.GetProperty("errorCode").ValueKind);
        Assert.True(json.GetProperty("local").GetBoolean());
        Assert.Equal("GatewayError", json.GetProperty("errors")[0].GetString()); // this application's own (localized) sentence
        Assert.DoesNotContain("raw English", json.GetRawText());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Unauthorized")]
    [InlineData(HttpStatusCode.Forbidden, "AccessDenied")]
    public async Task A_codeless_401_or_403_is_said_with_this_applications_own_sentence(HttpStatusCode status, string key)
    {
        var controller = UserRoleAssignments(new FixedGateway(status, ""));

        var json = Body(await controller.Revoke(OtherId, RoleId));

        Assert.True(json.GetProperty("local").GetBoolean());
        Assert.Equal(key, json.GetProperty("errors")[0].GetString());
    }

    [Fact]
    public async Task A_body_that_is_not_an_envelope_is_a_gateway_error_not_a_crash()
    {
        var controller = RoleAssignments(new FixedGateway(HttpStatusCode.BadGateway, "<html>502</html>"));

        var json = Body(await controller.Assign(RoleId, OtherId));

        Assert.Equal("GatewayError", json.GetProperty("errors")[0].GetString());
        Assert.DoesNotContain("502", json.GetRawText());
    }

    private static JsonElement Body(IActionResult result)
        => JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value)).RootElement;

    private static IConfiguration Configuration()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();

    private static RolesController Roles(FixedGateway gateway)
        => WithTenant(new RolesController(new HttpClient(gateway), Configuration(), new KeyLocalizer(), NullLogger<RolesController>.Instance));

    private static RoleAssignmentsController RoleAssignments(FixedGateway gateway)
        => WithTenant(new RoleAssignmentsController(new HttpClient(gateway), Configuration(), new KeyLocalizer(), NullLogger<RoleAssignmentsController>.Instance));

    private static UserRoleAssignmentsController UserRoleAssignments(FixedGateway gateway)
        => WithTenant(new UserRoleAssignmentsController(new HttpClient(gateway), Configuration(), new KeyLocalizer(), NullLogger<UserRoleAssignmentsController>.Instance));

    private static T WithTenant<T>(T controller) where T : Controller
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenantId", TenantId.ToString())], "test"))
            }
        };
        return controller;
    }

    private sealed class FixedGateway(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") });
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
