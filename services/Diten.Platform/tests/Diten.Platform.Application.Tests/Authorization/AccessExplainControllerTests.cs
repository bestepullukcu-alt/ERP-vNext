using System.Reflection;
using Diten.Platform.API.Authorization.Explain;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

// AG-STEP-011 / MOD-0018-FU14 Group B — the self-explain controller is plain [Authorize] (no [HasPermission], no actor
// policy, no subject/tenant parameter) and bounded to (permissionKey, moduleCode, featureCode).
public sealed class AccessExplainControllerTests
{
    private static readonly Type Controller = typeof(AccessExplainController);
    private static readonly MethodInfo Action = Controller.GetMethod(nameof(AccessExplainController.ExplainMyAccess))!;

    [Fact]
    public void Route_is_access_explain()
    {
        var route = Controller.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(route);
        Assert.Equal("api/platform/access/explain", route!.Template);
    }

    [Fact]
    public void Action_is_get_me()
    {
        var get = Action.GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(get);
        Assert.Equal("me", get!.Template);
    }

    [Fact]
    public void Action_has_plain_authorize()
    {
        var authorize = Action.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        // Plain [Authorize]: no policy, no roles, no scheme restriction.
        Assert.True(string.IsNullOrEmpty(authorize!.Policy));
        Assert.True(string.IsNullOrEmpty(authorize.Roles));
    }

    [Fact]
    public void Controller_and_action_have_no_haspermission_or_actor_policy()
    {
        var allAttrs = Controller.GetCustomAttributes(inherit: true)
            .Concat(Action.GetCustomAttributes(inherit: true))
            .Select(a => a.GetType().Name)
            .ToList();
        Assert.DoesNotContain("HasPermissionAttribute", allAttrs);

        // No actor-restricting authorize policy anywhere on the controller/action.
        var policies = Controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(Action.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .Select(a => a.Policy)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();
        Assert.Empty(policies);
    }

    [Fact]
    public void Action_params_are_bounded_no_subject_or_tenant()
    {
        var paramNames = Action.GetParameters().Select(p => p.Name).ToList();
        Assert.Contains("permissionKey", paramNames);
        Assert.Contains("moduleCode", paramNames);
        Assert.Contains("featureCode", paramNames);
        // No caller-controlled subject or tenant parameter.
        Assert.DoesNotContain("subjectUserId", paramNames);
        Assert.DoesNotContain("userId", paramNames);
        Assert.DoesNotContain("tenantId", paramNames);
    }

    [Fact]
    public async Task Action_delegates_to_the_service_and_maps_the_envelope()
    {
        var dto = new SelfAccessExplainResponse(
            "self", true, "platform.administrators.read", "canonical", false, "tenant_user",
            Guid.NewGuid(), ["OrgUnit"], new Dictionary<string, int> { ["OrgUnit"] = 1 },
            ["data-scope-opt-in-per-resource"], null, ["refresh-required-after-grant-change"], false);
        var service = new Mock<ISelfAccessExplainService>();
        service
            .Setup(s => s.ExplainAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), "platform.administrators.read", "MOD-0018", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<SelfAccessExplainResponse>.Success(dto, 200));

        var controller = new AccessExplainController(service.Object)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext(),
            },
        };

        var result = await controller.ExplainMyAccess("platform.administrators.read", "MOD-0018", null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<Response<SelfAccessExplainResponse>>(ok.Value);
        Assert.True(body.IsSuccessful);
        Assert.Equal("self", body.Data!.Mode);
        service.VerifyAll();
    }
}
