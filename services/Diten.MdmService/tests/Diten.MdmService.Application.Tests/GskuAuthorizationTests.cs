using System.Reflection;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GskuAuthorizationTests
{
    [Theory]
    [InlineData(nameof(GskusController.GetAll), "mdm.gskus.read")]
    [InlineData(nameof(GskusController.GetById), "mdm.gskus.read")]
    [InlineData(nameof(GskusController.GetCreateOptions), "mdm.gskus.create")]
    [InlineData(nameof(GskusController.CreateDraft), "mdm.gskus.create")]
    public void Every_endpoint_demands_the_exact_permission(string methodName, string permission)
    {
        var method = typeof(GskusController).GetMethod(methodName)!;
        var attribute = Assert.Single(method.GetCustomAttributes<HasPermissionAttribute>());
        Assert.Equal($"Permission:{permission}", attribute.Policy);
        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void Controller_is_authenticated_and_declares_no_manage_permission()
    {
        Assert.NotNull(typeof(GskusController).GetCustomAttribute<AuthorizeAttribute>());
        var policies = typeof(GskusController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(x => x.GetCustomAttributes<HasPermissionAttribute>())
            .Select(x => x.Policy)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(["Permission:mdm.gskus.create", "Permission:mdm.gskus.read"],
            policies.OrderBy(x => x, StringComparer.Ordinal));
    }
}
