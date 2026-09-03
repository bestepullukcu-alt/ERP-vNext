using System.Reflection;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

public sealed class PlatformTenantWorkflowDefinitionsControllerTests
{
    [Fact]
    public void Controller_has_exact_policy_route_and_six_bounded_endpoints()
    {
        var type = typeof(PlatformTenantWorkflowDefinitionsController);
        Assert.Equal("PlatformAdminOnly", type.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal("api/platform/tenants/{tenantId:guid}/workflow/definitions",
            type.GetCustomAttribute<RouteAttribute>()?.Template);

        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToArray();
        Assert.Equal(6, methods.Length);
        Assert.DoesNotContain(methods, method => method.GetCustomAttributes()
            .Any(attribute => attribute is HttpPutAttribute or HttpPatchAttribute or HttpDeleteAttribute));

        AssertPermission(methods, nameof(PlatformTenantWorkflowDefinitionsController.Create), WorkflowPermissions.DefinitionsManage);
        AssertPermission(methods, nameof(PlatformTenantWorkflowDefinitionsController.Publish), WorkflowPermissions.DefinitionsPublish);
        foreach (var method in methods.Where(method => method.Name.StartsWith("Get", StringComparison.Ordinal)))
        {
            AssertPermission(methods, method.Name, WorkflowPermissions.DefinitionsView);
        }
    }

    [Fact]
    public void Request_contracts_do_not_accept_tenant_or_actor_authority()
    {
        foreach (var type in new[] { typeof(CreateWorkflowDefinitionRequest), typeof(PublishWorkflowDefinitionRequest) })
        {
            Assert.Null(type.GetProperty("TenantId"));
            Assert.Null(type.GetProperty("ActorId"));
            Assert.Null(type.GetProperty("CreatedBy"));
            Assert.Null(type.GetProperty("PublishedBy"));
        }
    }

    private static void AssertPermission(MethodInfo[] methods, string name, string permission)
    {
        var method = Assert.Single(methods, candidate => candidate.Name == name);
        Assert.Equal(permission, method.GetCustomAttribute<HasPermissionAttribute>()?.Permission);
    }
}
